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
    internal sealed class ArasAmlClient : IArasAmlClient
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

                if (IsEmptyCollectionFault(fault, action, itemId))
                {
                    return new JObject
                    {
                        ["Items"] = new JArray()
                    };
                }

                throw ClassifySoapFault(fault, action, itemType, itemId);
            }

            var resultContent = ExtractXmlElement(soapXml, "Result");
            if (string.IsNullOrWhiteSpace(resultContent))
                return new JObject();

            return ParseResultAsItems(resultContent);
        }

        internal static bool IsEmptyCollectionFault(string fault, string action, string itemId)
        {
            if (!string.Equals(action, "get", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(itemId))
                return false;

            if (string.IsNullOrWhiteSpace(fault))
                return false;

            return fault.IndexOf("No items of type", StringComparison.OrdinalIgnoreCase) >= 0
                || fault.IndexOf("No items found", StringComparison.OrdinalIgnoreCase) >= 0;
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
                throw ClassifySoapFault(fault, methodName, "Method", null);
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
                throw ClassifySoapFault(fault, action, itemType, itemId);
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

        internal static ArasOperationException ClassifySoapFault(string faultText, string action, string itemType, string itemId)
        {
            if (faultText.IndexOf("locked by", StringComparison.OrdinalIgnoreCase) >= 0 ||
                faultText.IndexOf("is locked", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new ArasOperationException(
                    ArasErrorCode.CadLocked,
                    faultText,
                    details: new Dictionary<string, string> { ["cadId"] = itemId, ["action"] = action });
            }

            if (faultText.IndexOf("No items of type", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var code = ClassifyNotFoundError(itemType);
                var errMsg = code == ArasErrorCode.PartNotFound
                    ? "Part not found: " + itemId
                    : "Item not found: " + itemId;
                return new ArasOperationException(
                    code,
                    errMsg,
                    details: new Dictionary<string, string> { ["itemId"] = itemId, ["action"] = action, ["itemType"] = itemType });
            }

            var errorCode = ClassifyErrorText(faultText);
            var message = action != null
                ? "AML action '" + action + "' on " + (itemType ?? "Method") + " failed: " + faultText
                : "Method failed: " + faultText;
            return new ArasOperationException(
                errorCode,
                message,
                details: new Dictionary<string, string> { ["itemId"] = itemId, ["action"] = action, ["itemType"] = itemType });
        }

        internal static ArasOperationException MapArasError(string errorText, string methodName)
        {
            var code = ClassifyErrorText(errorText);
            return new ArasOperationException(code, "Method '" + methodName + "' failed: " + errorText);
        }

        internal static ArasErrorCode ClassifyErrorText(string errorText)
        {
            var upper = (errorText ?? string.Empty).ToUpperInvariant();

            if (upper.IndexOf("ACCESS DENIED", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("PERMISSION", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("NOT AUTHORIZED", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("UNAUTHORIZED", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("INSUFFICIENT PERMISSION", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("NO PERMISSION", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("NOT ALLOWED TO PERFORM", StringComparison.Ordinal) >= 0)
                return ArasErrorCode.PermissionDenied;

            if (upper.IndexOf("SESSION EXPIRED", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("TOKEN EXPIRED", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("AUTHENTICATION EXPIRED", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("EXPIRED SESSION", StringComparison.Ordinal) >= 0)
                return ArasErrorCode.AuthExpired;

            if (upper.IndexOf("COULD NOT LOG IN", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("AUTHENTICATION FAILED", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("INVALID CREDENTIALS", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("NOT AUTHENTICATED", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("INVALID TOKEN", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("INVALID SESSION", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("LOGIN REQUIRED", StringComparison.Ordinal) >= 0)
                return ArasErrorCode.AuthInvalid;

            if (upper.IndexOf("SERVER WAS UNABLE", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("INTERNAL SERVER ERROR", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("SERVER UNAVAILABLE", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("SERVICE UNAVAILABLE", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("CONNECTION REFUSED", StringComparison.Ordinal) >= 0 ||
                upper.IndexOf("GATEWAY TIMEOUT", StringComparison.Ordinal) >= 0)
                return ArasErrorCode.ServerUnavailable;

            if (upper.StartsWith("PART_NOT_FOUND"))
                return ArasErrorCode.PartNotFound;

            if (upper.StartsWith("CAD_NOT_FOUND"))
                return ArasErrorCode.CadNotFound;

            if (upper.StartsWith("CAD_CREATE_FAILED") || upper.StartsWith("CAD_ALREADY_EXISTS"))
                return ArasErrorCode.CadAlreadyExists;

            if (upper.StartsWith("CAD_LOCKED"))
                return ArasErrorCode.CadLocked;

            if (upper.StartsWith("VALIDATION_FAILED"))
                return ArasErrorCode.ValidationFailed;

            return ArasErrorCode.UnexpectedServerError;
        }

        internal static ArasErrorCode ClassifyNotFoundError(string itemType)
        {
            if (string.Equals(itemType, "Part", StringComparison.OrdinalIgnoreCase))
                return ArasErrorCode.PartNotFound;

            if (string.Equals(itemType, "CAD", StringComparison.OrdinalIgnoreCase))
                return ArasErrorCode.CadNotFound;

            return ArasErrorCode.ValidationFailed;
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
