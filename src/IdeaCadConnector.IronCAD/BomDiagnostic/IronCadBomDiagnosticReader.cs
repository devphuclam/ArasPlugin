using System;
using System.Collections.Generic;
using interop.ICApiIronCAD;
using IdeaCadConnector.Workspace.BomDiagnostic;

namespace IdeaCadConnector.IronCAD.BomDiagnostic
{
    public sealed class IronCadBomDiagnosticReadResult
    {
        public string DocumentName { get; set; }
        public string AuthoringToolVersion { get; set; }
        public string ActiveDocumentType { get; set; }
        public bool TopElementAvailable { get; set; }
        public BomDiagnosticSourceNode RootNode { get; set; }
        public IList<string> Warnings { get; } = new List<string>();
    }

    public sealed class IronCadBomDiagnosticReader
    {
        public IronCadBomDiagnosticReadResult Read(IZBaseApp application)
        {
            var result = new IronCadBomDiagnosticReadResult();
            if (application == null)
            {
                result.Warnings.Add("IronCAD application is unavailable.");
                return result;
            }

            IZDoc document;
            try
            {
                document = application.ActiveDoc;
                result.AuthoringToolVersion = application.ApplicationVersion;
            }
            catch (Exception ex)
            {
                result.Warnings.Add("Active document could not be read: " + ex.Message);
                return result;
            }

            if (document == null)
            {
                result.Warnings.Add("No active IronCAD document is available.");
                return result;
            }

            TryReadDocumentInfo(document, result);
            var scene = document as IZSceneDoc;
            if (scene == null)
            {
                result.Warnings.Add("The active document is not a scene document.");
                return result;
            }

            IZElement top;
            try
            {
                top = scene.GetTopElement();
                result.TopElementAvailable = top != null;
            }
            catch (Exception ex)
            {
                result.Warnings.Add("Top element could not be read: " + ex.Message);
                return result;
            }

            if (top == null)
            {
                result.Warnings.Add("The scene has no top element.");
                return result;
            }

            result.RootNode = ReadElement(top, null, result.Warnings);
            return result;
        }

        private static void TryReadDocumentInfo(IZDoc document, IronCadBomDiagnosticReadResult result)
        {
            try { result.DocumentName = document.Name; }
            catch (Exception ex) { result.Warnings.Add("Document name is unavailable: " + ex.Message); }
            try { result.ActiveDocumentType = document.Type.ToString(); }
            catch (Exception ex) { result.Warnings.Add("Document type is unavailable: " + ex.Message); }
        }

        private static BomDiagnosticSourceNode ReadElement(
            IZElement element,
            string parentRuntimeId,
            IList<string> warnings)
        {
            var node = new BomDiagnosticSourceNode
            {
                Children = new List<BomDiagnosticSourceNode>()
            };
            node.RuntimeId = TryRead(() => element.Id.ToString(), "runtime ID", warnings);
            node.DisplayName = TryRead(() => element.Name, "scene name", warnings);
            node.NodeKind = TryRead(() => element.Type.ToString(), "element type", warnings);
            node.IsSuppressed = TryReadNullable(() => element.GetStateStatus(eZElementState.Z_SUPPRESSED), "suppressed state", warnings);

            var part = element as IZPart;
            var assembly = element as IZAssembly;
            if (part != null)
            {
                node.IsVisible = TryReadNullable(() => !part.IsHidden, "part visibility", warnings);
                node.IncludedInBom = TryReadNullable(() => part.IncludedInBOM, "part BOM inclusion", warnings);
                ReadExternalInfo(part, node, warnings);
            }
            else if (assembly != null)
            {
                node.IsVisible = TryReadNullable(() => !assembly.IsHidden, "assembly visibility", warnings);
                node.IncludedInBom = TryReadNullable(() => assembly.IncludedInBOM, "assembly BOM inclusion", warnings);
                ReadExternalInfo(assembly, node, warnings);
            }

            try
            {
                var customProperties = element.GetCustomPropManager(1);
                node.CustomPropertyCount = customProperties == null ? 0 : customProperties.Count;
            }
            catch (Exception ex)
            {
                warnings.Add("Custom properties unavailable for node '" + (node.RuntimeId ?? "<missing>") + "': " + ex.Message);
            }

            try
            {
                var sceneElement = element as IZSceneElement;
                if (sceneElement != null)
                {
                    var path = sceneElement.ModelLinkPath;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        node.ExternalFilePath = path;
                        node.IsExternal = true;
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add("Model link path unavailable for node '" + (node.RuntimeId ?? "<missing>") + "': " + ex.Message);
            }

            try
            {
                var children = element.GetChildrenZArray();
                int count = 0;
                if (children != null)
                {
                    children.Count(out count);
                    for (var index = 0; index < count; index++)
                    {
                        object childObject;
                        children.Get(index, out childObject);
                        var child = childObject as IZElement;
                        if (child == null)
                        {
                            warnings.Add("Unsupported child object under node '" + (node.RuntimeId ?? "<missing>") + "'.");
                            continue;
                        }
                        node.Children.Add(ReadElement(child, node.RuntimeId, warnings));
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add("Children unavailable for node '" + (node.RuntimeId ?? "<missing>") + "': " + ex.Message);
            }
            return node;
        }

        private static void ReadExternalInfo(IZPart part, BomDiagnosticSourceNode node, IList<string> warnings)
        {
            try
            {
                bool linked;
                var path = part.GetExternallyLinkedInfo(out linked);
                if (linked) node.IsExternal = true;
                if (!string.IsNullOrWhiteSpace(path)) node.ExternalFilePath = path;
            }
            catch (Exception ex) { warnings.Add("Part external-link info unavailable: " + ex.Message); }
        }

        private static void ReadExternalInfo(IZAssembly assembly, BomDiagnosticSourceNode node, IList<string> warnings)
        {
            try
            {
                bool linked;
                var path = assembly.GetExternallyLinkedInfo(out linked);
                if (linked) node.IsExternal = true;
                if (!string.IsNullOrWhiteSpace(path)) node.ExternalFilePath = path;
            }
            catch (Exception ex) { warnings.Add("Assembly external-link info unavailable: " + ex.Message); }
        }

        private static T TryRead<T>(Func<T> read, string label, IList<string> warnings)
        {
            try { return read(); }
            catch (Exception ex) { warnings.Add(label + " unavailable: " + ex.Message); return default(T); }
        }

        private static bool? TryReadNullable(Func<bool> read, string label, IList<string> warnings)
        {
            try { return read(); }
            catch (Exception ex) { warnings.Add(label + " unavailable: " + ex.Message); return null; }
        }
    }
}
