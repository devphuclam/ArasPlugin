using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using IdeaCadConnector.Core.Errors;
using Newtonsoft.Json.Linq;

namespace IdeaCadConnector.Aras
{
    internal sealed class ArasAmlClient
    {
        private readonly ArasHttpClient _http;
        private readonly string _database;

        public ArasAmlClient(ArasHttpClient http, string database)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _database = database ?? string.Empty;
        }

        public async Task<JObject> ApplyMethodAsync(string methodName, IDictionary<string, string> parameters, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(methodName))
                throw new ArgumentException("Method name is required.", nameof(methodName));

            var amlBody = BuildMethodAml(methodName, parameters);
            var soapEnvelope =
                "<SOAP-ENV:Envelope xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                "<SOAP-ENV:Body><ApplyMethod>" + amlBody + "</ApplyMethod></SOAP-ENV:Body>" +
                "</SOAP-ENV:Envelope>";

            using var content = new System.Net.Http.StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            var headers = new Dictionary<string, string>
            {
                ["SOAPAction"] = "ApplyMethod",
                ["DATABASE"] = _database
            };

            var soapXml = await _http.PostXmlAsync("Server/InnovatorServer.aspx", content, headers, ct).ConfigureAwait(false);
            return ParseSoapMethodResponse(soapXml, methodName);
        }

        public async Task<JObject> ApplyItemAsync(
            string itemType,
            string itemId,
            string action,
            string selectFields,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(itemType))
                throw new ArgumentException("Item type is required.", nameof(itemType));
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("Item id is required.", nameof(itemId));
            if (string.IsNullOrWhiteSpace(action))
                throw new ArgumentException("Action is required.", nameof(action));

            var amlBody = BuildItemActionAml(itemType, itemId, action, selectFields);
            return await SendAmlAsync(amlBody, "ApplyItem", action, itemType, itemId, ct);
        }

        public async Task<JObject> ApplyAmlAsync(string amlBody, string action, string itemType, string itemId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(amlBody))
                throw new ArgumentException("AML body is required.", nameof(amlBody));

            var soapAction = "ApplyItem";
            if (amlBody.Contains("action=\"get\""))
                soapAction = "ApplyItem";

            return await SendAmlAsync(amlBody, soapAction, action, itemType, itemId, ct);
        }

        private async Task<JObject> SendAmlAsync(string amlBody, string soapAction, string action, string itemType, string itemId, CancellationToken ct)
        {
            var soapEnvelope =
                "<SOAP-ENV:Envelope xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                "<SOAP-ENV:Body><" + soapAction + ">" + amlBody + "</" + soapAction + "></SOAP-ENV:Body>" +
                "</SOAP-ENV:Envelope>";

            using var content = new System.Net.Http.StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            var headers = new Dictionary<string, string>
            {
                ["SOAPAction"] = soapAction,
                ["DATABASE"] = _database
            };

            var soapXml = await _http.PostXmlAsync("Server/InnovatorServer.aspx", content, headers, ct).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(soapXml))
                return new JObject();

            if (soapXml.IndexOf("<faultstring>", StringComparison.OrdinalIgnoreCase) >= 0 ||
                soapXml.IndexOf("<faultcode>", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var fault = ExtractXmlElement(soapXml, "faultstring") ?? "Unknown SOAP fault";

                if (fault.IndexOf("locked by", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fault.IndexOf("is locked", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new ArasOperationException(
                        ArasErrorCode.CadLocked,
                        fault,
                        details: new Dictionary<string, string> { ["cadId"] = itemId, ["action"] = action });
                }

                if (fault.IndexOf("No items of type", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new ArasOperationException(
                        ArasErrorCode.CadNotFound,
                        "Item not found: " + itemId,
                        details: new Dictionary<string, string> { ["itemId"] = itemId, ["action"] = action });
                }

                throw new ArasOperationException(
                    ArasErrorCode.UnexpectedServerError,
                    "AML action '" + action + "' on " + itemType + " failed: " + fault,
                    details: new Dictionary<string, string> { ["itemId"] = itemId, ["action"] = action });
            }

            var resultContent = ExtractXmlElement(soapXml, "Result");
            if (string.IsNullOrWhiteSpace(resultContent))
                return new JObject();

            return ParseResultAsItems(resultContent);
        }

        private static JObject ParseResultAsItems(string resultContent)
        {
            var doc = new XmlDocument();
            doc.LoadXml("<Root>" + resultContent + "</Root>");

            var items = doc.SelectNodes("//Item");
            if (items == null || items.Count == 0)
                return new JObject();

            var parsedItems = new JArray();
            foreach (XmlNode itemNode in items)
            {
                if (itemNode.NodeType != XmlNodeType.Element)
                    continue;

                parsedItems.Add(ParseItemNode(itemNode));
            }

            var result = parsedItems.Count > 0
                ? (JObject)parsedItems[0].DeepClone()
                : new JObject();
            result["Items"] = parsedItems;
            return result;
        }

        private static JObject ParseItemNode(XmlNode itemNode)
        {
            var result = new JObject();

            if (itemNode.Attributes != null)
            {
                foreach (XmlAttribute attr in itemNode.Attributes)
                {
                    result[attr.Name] = attr.Value;
                }
            }

            foreach (XmlNode child in itemNode.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element)
                    continue;

                if (child.Name == "Relationships")
                {
                    var relArray = new JArray();
                    foreach (XmlNode relItem in child.ChildNodes)
                    {
                        if (relItem.NodeType == XmlNodeType.Element)
                            relArray.Add(ParseItemNode(relItem));
                    }
                    result["Relationships"] = relArray;
                }
                else
                {
                    result[child.Name] = child.InnerText;
                }
            }

            return result;
        }

        private static string BuildMethodAml(string methodName, IDictionary<string, string> parameters)
        {
            var sb = new StringBuilder();
            sb.Append("<Item type=\"Method\" action=\"").Append(EscapeXml(methodName)).Append("\">");

            if (parameters != null)
            {
                foreach (var pair in parameters)
                {
                    if (pair.Value == null)
                        continue;

                    sb.Append("<").Append(EscapeXml(pair.Key)).Append(">");
                    sb.Append(EscapeXml(pair.Value));
                    sb.Append("</").Append(EscapeXml(pair.Key)).Append(">");
                }
            }

            sb.Append("</Item>");
            return sb.ToString();
        }

        private static string BuildItemActionAml(string itemType, string itemId, string action, string selectFields)
        {
            var sb = new StringBuilder();
            sb.Append("<Item type=\"")
                .Append(EscapeXml(itemType))
                .Append("\" id=\"")
                .Append(EscapeXml(itemId))
                .Append("\" action=\"")
                .Append(EscapeXml(action))
                .Append("\"");

            if (!string.IsNullOrWhiteSpace(selectFields))
            {
                sb.Append(" select=\"").Append(EscapeXml(selectFields)).Append("\"");
            }

            sb.Append(" />");
            return sb.ToString();
        }

        private static JObject ParseSoapMethodResponse(string soapXml, string methodName)
        {
            if (string.IsNullOrWhiteSpace(soapXml))
                return new JObject();

            if (soapXml.IndexOf("<faultstring>", StringComparison.OrdinalIgnoreCase) >= 0 ||
                soapXml.IndexOf("<faultcode>", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var fault = ExtractXmlElement(soapXml, "faultstring") ?? "Unknown SOAP fault";
                throw MapArasError(fault, methodName);
            }

            var resultContent = ExtractXmlElement(soapXml, "Result");
            if (string.IsNullOrWhiteSpace(resultContent))
                return new JObject();

            var itemStart = resultContent.IndexOf("<Item ", StringComparison.Ordinal);
            if (itemStart < 0)
                itemStart = resultContent.IndexOf("<Item>", StringComparison.Ordinal);

            if (itemStart < 0)
                return new JObject { ["value"] = resultContent };

            return ParseItemProperties(resultContent);
        }

        private static JObject ParseSoapItemResponse(string soapXml, string itemType, string itemId, string action)
        {
            if (string.IsNullOrWhiteSpace(soapXml))
                return new JObject();

            if (soapXml.IndexOf("<faultstring>", StringComparison.OrdinalIgnoreCase) >= 0 ||
                soapXml.IndexOf("<faultcode>", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var fault = ExtractXmlElement(soapXml, "faultstring") ?? "Unknown SOAP fault";

                if (fault.IndexOf("locked by", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fault.IndexOf("is locked", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new ArasOperationException(
                        ArasErrorCode.CadLocked,
                        fault,
                        details: new Dictionary<string, string> { ["cadId"] = itemId, ["action"] = action });
                }

                if (fault.IndexOf("No items of type", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new ArasOperationException(
                        ArasErrorCode.CadNotFound,
                        "CAD item not found: " + itemId,
                        details: new Dictionary<string, string> { ["cadId"] = itemId, ["action"] = action });
                }

                throw new ArasOperationException(
                    ArasErrorCode.UnexpectedServerError,
                    "AML action '" + action + "' on " + itemType + " failed: " + fault,
                    details: new Dictionary<string, string> { ["cadId"] = itemId, ["action"] = action });
            }

            var resultContent = ExtractXmlElement(soapXml, "Result");
            if (string.IsNullOrWhiteSpace(resultContent))
                return new JObject();

            return ParseItemProperties(resultContent);
        }

        private static JObject ParseItemProperties(string itemXml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(itemXml);

            var item = doc.SelectSingleNode("//Item");
            if (item == null)
                return new JObject();

            var result = new JObject();

            if (item.Attributes != null)
            {
                foreach (XmlAttribute attr in item.Attributes)
                {
                    if (attr.Name == "id" || attr.Name == "type")
                        result[attr.Name] = attr.Value;
                }
            }

            foreach (XmlNode child in item.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element)
                    continue;

                result[child.Name] = child.InnerText;
            }

            return result;
        }

        private static string ExtractXmlElement(string xml, string elementName)
        {
            var openTag = "<" + elementName;
            var closeTag = "</" + elementName + ">";
            var start = xml.IndexOf(openTag, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return null;

            start = xml.IndexOf(">", start, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return null;

            start++;
            var end = xml.IndexOf(closeTag, start, StringComparison.OrdinalIgnoreCase);
            if (end < 0)
                return null;

            return xml.Substring(start, end - start);
        }

        private static ArasOperationException MapArasError(string errorText, string methodName)
        {
            var upper = (errorText ?? string.Empty).ToUpperInvariant();
            ArasErrorCode code;

            if (upper.StartsWith("PART_NOT_FOUND"))
                code = ArasErrorCode.PartNotFound;
            else if (upper.StartsWith("CAD_NOT_FOUND"))
                code = ArasErrorCode.CadNotFound;
            else if (upper.StartsWith("CAD_CREATE_FAILED") || upper.StartsWith("CAD_ALREADY_EXISTS"))
                code = ArasErrorCode.CadAlreadyExists;
            else if (upper.StartsWith("CAD_LOCKED"))
                code = ArasErrorCode.CadLocked;
            else if (upper.StartsWith("VALIDATION_FAILED"))
                code = ArasErrorCode.ValidationFailed;
            else
                code = ArasErrorCode.UnexpectedServerError;

            return new ArasOperationException(code, "Method '" + methodName + "' failed: " + errorText);
        }

        private static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
