using System;
using System.Collections.Generic;
using interop.ICApiIronCAD;

namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public sealed class IronCadExternalReferenceReader
    {
        public IReadOnlyList<IronCadExternalReferenceRecord> Read(IZSceneDoc scene)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));

            var records = new List<IronCadExternalReferenceRecord>();
            var top = scene.GetTopElement();
            if (top == null) return records;

            ReadElement(top, "0", records);
            return records;
        }

        private static void ReadElement(IZElement element, string occurrencePath, List<IronCadExternalReferenceRecord> records)
        {
            var sceneElement = element as IZSceneElement;
            string link = null;
            if (sceneElement != null)
            {
                try { link = sceneElement.ModelLinkPath; }
                catch { }
            }

            var part = element as IZPart;
            if (part != null)
            {
                bool linked;
                var p = part.GetExternallyLinkedInfo(out linked);
                if (linked && !string.IsNullOrWhiteSpace(p)) link = p;
            }

            var assembly = element as IZAssembly;
            if (assembly != null)
            {
                bool linked;
                var p = assembly.GetExternallyLinkedInfo(out linked);
                if (linked && !string.IsNullOrWhiteSpace(p)) link = p;
            }

            records.Add(new IronCadExternalReferenceRecord
            {
                OccurrencePath = occurrencePath,
                ReportedLinkPath = link
            });

            IZArray children;
            try { children = element.GetChildrenZArray(); }
            catch { return; }
            if (children == null) return;

            int count;
            children.Count(out count);
            for (var i = 0; i < count; i++)
            {
                object value;
                try { children.Get(i, out value); }
                catch { continue; }
                var child = value as IZElement;
                if (child == null) continue;
                ReadElement(child, occurrencePath + "/" + i, records);
            }
        }
    }
}
