using System;
using System.Threading;
using System.Threading.Tasks;
using interop.ICApiIronCAD;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;

namespace IdeaCadConnector.IronCAD
{
    public sealed class IronCadCadAdapter : ICadApplicationAdapter
    {
        private readonly IronCadAddin _addin;

        public IronCadCadAdapter(IronCadAddin addin)
        {
            _addin = addin ?? throw new ArgumentNullException(nameof(addin));
        }

        private IZBaseApp IronCADApp
        {
            get { return _addin.IronCADApp; }
        }

        public string AuthoringTool
        {
            get { return CadConstants.IronCadAuthoringTool; }
        }

        public string AuthoringToolVersion
        {
            get
            {
                try
                {
                    return "IronCAD " + IronCADApp.ApiVersion;
                }
                catch
                {
                    return "IronCAD (unknown version)";
                }
            }
        }

        public CadDocumentInfo GetActiveDocumentInfo()
        {
            var doc = IronCADApp?.ActiveDoc;
            if (doc == null)
                return null;

            try
            {
                var fileName = doc.Name ?? "";
                return new CadDocumentInfo
                {
                    FullPath = fileName,
                    FileName = System.IO.Path.GetFileName(fileName),
                    Extension = System.IO.Path.GetExtension(fileName),
                    IsDirty = doc.Modified
                };
            }
            catch
            {
                return null;
            }
        }

        public CadMetadata ReadMetadata()
        {
            var metadata = new CadMetadata
            {
                AuthoringTool = CadConstants.IronCadAuthoringTool,
                AuthoringToolVersion = AuthoringToolVersion
            };

            var doc = IronCADApp?.ActiveDoc;
            if (doc == null)
                return metadata;

            var sceneDoc = doc as IZSceneDoc;
            if (sceneDoc == null)
                return metadata;

            try
            {
                var topElem = sceneDoc.GetTopElement();
                if (topElem == null)
                    return metadata;

                var propMgr = topElem.GetCustomPropManager(1);
                if (propMgr == null)
                    return metadata;

                string value;
                bool found;

                propMgr.GetCustomPropAsString("PartId", out value, out found);
                if (found) metadata.PartId = value;

                propMgr.GetCustomPropAsString("PartNumber", out value, out found);
                if (found) metadata.PartNumber = value;

                propMgr.GetCustomPropAsString("PartType", out value, out found);
                if (found) metadata.PartType = value;

                propMgr.GetCustomPropAsString("CadId", out value, out found);
                if (found) metadata.CadId = value;

                propMgr.GetCustomPropAsString("CadNumber", out value, out found);
                if (found) metadata.CadNumber = value;

                propMgr.GetCustomPropAsString("Classification", out value, out found);
                if (found) metadata.Classification = value;

                propMgr.GetCustomPropAsString("MassUnit", out value, out found);
                if (found) metadata.MassUnit = value;

                propMgr.GetCustomPropAsString("Description", out value, out found);
                if (found) metadata.Description = value;

                propMgr.GetCustomPropAsString("Revision", out value, out found);
                if (found) metadata.Revision = value;

                propMgr.GetCustomPropAsString("Material", out value, out found);
                if (found) metadata.Material = value;

                propMgr.GetCustomPropAsString("State", out value, out found);
                if (found) metadata.State = value;

                string massStr;
                bool massFound;
                propMgr.GetCustomPropAsString("Mass", out massStr, out massFound);
                if (massFound && !string.IsNullOrWhiteSpace(massStr))
                {
                    decimal massVal;
                    if (decimal.TryParse(massStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out massVal))
                    {
                        metadata.Mass = massVal;
                    }
                }
            }
            catch
            {
                // Return whatever metadata was read so far
            }

            return metadata;
        }

        public void WriteMetadata(CadMetadata metadata)
        {
            if (metadata == null)
                return;

            var doc = IronCADApp?.ActiveDoc;
            if (doc == null)
                return;

            var sceneDoc = doc as IZSceneDoc;
            if (sceneDoc == null)
                return;

            try
            {
                var topElem = sceneDoc.GetTopElement();
                if (topElem == null)
                    return;

                var propMgr = topElem.GetCustomPropManager(1);
                if (propMgr == null)
                    return;

                SetProp(propMgr, "PartId", metadata.PartId);
                SetProp(propMgr, "PartNumber", metadata.PartNumber);
                SetProp(propMgr, "PartType", metadata.PartType);
                SetProp(propMgr, "CadId", metadata.CadId);
                SetProp(propMgr, "CadNumber", metadata.CadNumber);
                SetProp(propMgr, "Classification", metadata.Classification);
                SetProp(propMgr, "MassUnit", metadata.MassUnit);
                SetProp(propMgr, "Description", metadata.Description);
                SetProp(propMgr, "Revision", metadata.Revision);
                SetProp(propMgr, "Material", metadata.Material);
                SetProp(propMgr, "State", metadata.State);

                if (metadata.Mass.HasValue)
                    SetProp(propMgr, "Mass", metadata.Mass.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            catch
            {
                // Best-effort write
            }
        }

        public Task OpenDocumentAsync(string filePath, CadOpenMode openMode, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required.", nameof(filePath));

            try
            {
                // Open the file in IronCAD using the ICAPI OpenFile method
                bool readOnly = openMode == CadOpenMode.ReadOnly;
                IronCADApp.OpenFile(filePath, readOnly);
            }
            catch
            {
                // File could not be opened — caller will notify user to open manually
            }

            return Task.FromResult(0);
        }

        private static void SetProp(IZCustomPropMgr propMgr, string name, string value)
        {
            if (string.IsNullOrEmpty(name))
                return;

            try
            {
                propMgr.AddCustomPropString(name, value ?? string.Empty,
                    interop.ICApiIronCAD.eZPropPersFlag.Z_PPO_PERS_FLAG_NONE, true);
            }
            catch
            {
                // Ignore individual property write failures
            }
        }
    }
}
