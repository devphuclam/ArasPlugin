using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using IdeaCadConnector.Core.Errors;
using Newtonsoft.Json.Linq;

namespace IdeaCadConnector.Aras
{
    /// <summary>
    /// Thin SOAP client for Aras Innovator ApplyMethod calls.
    /// Uses the classic /server/soap.aspx endpoint so methods can be invoked by name
    /// instead of by GUID (which OData requires).
    /// </summary>
    internal sealed class ArasSoapClient
    {
        private readonly ArasHttpClient _http;

        public ArasSoapClient(ArasHttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        /// <summary>
        /// Calls a server method by name with the supplied XML body fragment.
        /// The bodyFragment is embedded inside a <body> CDATA section.
        /// </summary>
        public async Task<JObject> ApplyMethodAsync(string methodName, string bodyFragment, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(methodName))
                throw new ArgumentException("Method name is required.", nameof(methodName));

            var envelope = BuildSoapEnvelope(methodName, bodyFragment);
            var content = new StringContent(envelope, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "ApplyMethod");

            var responseXml = await _http.PostXmlAsync("server/soap.aspx", content, ct).ConfigureAwait(false);
            return ParseSoapResponse(responseXml, methodName);
        }

        /// <summary>
        /// Convenience overload: build the body from a dictionary of parameters.
        /// </summary>
        public async Task<JObject> ApplyMethodAsync(string methodName, IDictionary<string, string> parameters, CancellationToken ct)
        {
            var bodyBuilder = new StringBuilder();
            if (parameters != null)
            {
                foreach (var pair in parameters)
                {
                    bodyBuilder.AppendFormat("<{0}>{1}</{0}>", pair.Key, EscapeXml(pair.Value));
                }
            }

            return await ApplyMethodAsync(methodName, bodyBuilder.ToString(), ct).ConfigureAwait(false);
        }

        private static string BuildSoapEnvelope(string methodName, string bodyFragment)
        {
            return
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">" +
                "<soap:Body>" +
                "<ApplyMethod xmlns=\"http://www.aras-corp.com/\">" +
                "<methodName>" + EscapeXml(methodName) + "</methodName>" +
                "<body><![CDATA[" + bodyFragment + "]]></body>" +
                "</ApplyMethod>" +
                "</soap:Body>" +
                "</soap:Envelope>";
        }

        private static JObject ParseSoapResponse(string responseXml, string methodName)
        {
            if (string.IsNullOrWhiteSpace(responseXml))
                throw new ArasOperationException(ArasErrorCode.UnexpectedServerError, $"Empty SOAP response for method {methodName}.");

            var doc = new XmlDocument();
            doc.LoadXml(responseXml);

            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
            nsmgr.AddNamespace("aras", "http://www.aras-corp.com/");

            // Check for SOAP Fault
            var faultNode = doc.SelectSingleNode("//soap:Fault", nsmgr) ?? doc.SelectSingleNode("//Fault");
            if (faultNode != null)
            {
                var faultString = faultNode.SelectSingleNode("faultstring")?.InnerText
                    ?? faultNode.SelectSingleNode("soap:faultstring", nsmgr)?.InnerText
                    ?? "SOAP fault";
                throw new ArasOperationException(ArasErrorCode.UnexpectedServerError, $"SOAP fault calling {methodName}: {faultString}");
            }

            var resultNode = doc.SelectSingleNode("//aras:ApplyMethodResponse/aras:Result", nsmgr)
                ?? doc.SelectSingleNode("//ApplyMethodResponse/Result")
                ?? doc.SelectSingleNode("//Result");

            if (resultNode == null)
                throw new ArasOperationException(ArasErrorCode.UnexpectedServerError, "SOAP response did not contain a Result node.");

            var resultXml = resultNode.InnerXml;
            if (string.IsNullOrWhiteSpace(resultXml))
                return new JObject();

            var json = XmlToJson(resultXml);
            if (json != null)
                return json;

            return new JObject { ["__xml"] = resultXml };
        }

        private static JObject XmlToJson(string xml)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xml);

                if (doc.DocumentElement == null)
                    return null;

                var root = doc.DocumentElement;
                var json = new JObject();

                foreach (XmlNode child in root.ChildNodes)
                {
                    if (child.NodeType != XmlNodeType.Element)
                        continue;

                    json[child.Name] = child.InnerText;
                }

                if (root.Attributes != null)
                {
                    foreach (XmlAttribute attr in root.Attributes)
                    {
                        json[attr.Name] = attr.Value;
                    }
                }

                return json;
            }
            catch
            {
                return null;
            }
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
