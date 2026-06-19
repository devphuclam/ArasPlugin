using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Errors;
using Newtonsoft.Json.Linq;

namespace IdeaCadConnector.Aras
{
    /// <summary>
    /// Thin vault client for Aras Innovator file upload/download.
    /// Uses the common Aras 12/15 vault endpoints.
    /// </summary>
    internal sealed class VaultClient
    {
        private readonly ArasHttpClient _http;
        private readonly ArasClientOptions _options;

        public VaultClient(ArasHttpClient http, ArasClientOptions options)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Uploads a local file to the Aras vault and returns the File item id.
        /// </summary>
        public async Task<string> UploadFileAsync(string filePath, string fileName, CancellationToken ct)
        {
            if (!File.Exists(filePath))
                throw new ArasOperationException(ArasErrorCode.FileUploadNotFound, $"File not found: {filePath}");

            fileName = string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(filePath) : fileName;
            var fileInfo = new FileInfo(filePath);
            var fileId = Guid.NewGuid().ToString("N").ToUpperInvariant();
            var boundary = "----ArasVault" + fileId;
            var soapBody =
                "<SOAP-ENV:Envelope xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                "<SOAP-ENV:Body>" +
                "<ApplyItem>" +
                "<Item type=\"File\" action=\"add\" id=\"" + fileId + "\">" +
                "<filename>" + EscapeXml(fileName) + "</filename>" +
                "<file_size>" + fileInfo.Length + "</file_size>" +
                "<Relationships>" +
                "<Item type=\"Located\" action=\"add\">" +
                "<file_version>1</file_version>" +
                "<related_id>" + _options.VaultId + "</related_id>" +
                "</Item>" +
                "</Relationships>" +
                "</Item>" +
                "</ApplyItem>" +
                "</SOAP-ENV:Body>" +
                "</SOAP-ENV:Envelope>";

            var crlf = "\r\n";
            var header = 
                "--" + boundary + crlf +
                "Content-Disposition: form-data; name=\"XMLdata\"" + crlf +
                "Content-Type: text/xml; charset=utf-8" + crlf + crlf +
                soapBody + crlf +
                "--" + boundary + crlf +
                "Content-Disposition: form-data; name=\"" + fileId + "\"; filename=\"" + fileName + "\"" + crlf +
                "Content-Type: application/octet-stream" + crlf + crlf;
            var footer = crlf + "--" + boundary + "--" + crlf;

            var headerBytes = Encoding.UTF8.GetBytes(header);
            var fileBytes = File.ReadAllBytes(filePath);
            var footerBytes = Encoding.UTF8.GetBytes(footer);
            var payload = new byte[headerBytes.Length + fileBytes.Length + footerBytes.Length];
            Buffer.BlockCopy(headerBytes, 0, payload, 0, headerBytes.Length);
            Buffer.BlockCopy(fileBytes, 0, payload, headerBytes.Length, fileBytes.Length);
            Buffer.BlockCopy(footerBytes, 0, payload, headerBytes.Length + fileBytes.Length, footerBytes.Length);

            using var content = new ByteArrayContent(payload);
            content.Headers.ContentType =
                System.Net.Http.Headers.MediaTypeHeaderValue.Parse("multipart/form-data; boundary=" + boundary);

            var headers = new Dictionary<string, string>
            {
                ["DATABASE"] = _options.Database,
                ["SOAPAction"] = "ApplyItem",
                ["VAULTID"] = _options.VaultId
            };

            var responseBody = await _http.PostRawAsync(
                new Uri(_options.BaseUri, "vault/vaultserver.aspx").ToString(),
                content,
                headers,
                ct).ConfigureAwait(false);

            if (responseBody.IndexOf("<faultstring>", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new ArasOperationException(
                    ArasErrorCode.UnexpectedServerError,
                    "Vault upload SOAP fault: " + responseBody);
            }

            return fileId;
        }

        /// <summary>
        /// Downloads a native file from the vault to the target directory and returns the local path.
        /// </summary>
        public async Task<string> DownloadFileAsync(string fileId, string targetDirectory, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(fileId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "File id is required.");

            Directory.CreateDirectory(targetDirectory);

            var fileItem = await _http.GetJsonAsync(
                $"server/odata/File?$filter=id eq '{EscapeId(fileId)}'&$select=filename",
                ct).ConfigureAwait(false);

            var fileName = fileItem["value"]?[0]?["filename"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = fileId;

            var targetPath = Path.Combine(targetDirectory, fileName);
            var bytes = await _http.GetBytesAsync($"server/odata/File('{EscapeId(fileId)}')/$value", ct).ConfigureAwait(false);

            File.WriteAllBytes(targetPath, bytes);
            return targetPath;
        }

        private static string EscapeId(string id)
        {
            return id?.Replace("'", "''");
        }

        private static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
