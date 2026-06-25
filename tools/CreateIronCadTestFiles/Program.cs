using System;
using System.IO;
using interop.ICApiIronCAD;

namespace CreateIronCadTestFiles
{
    internal static class Program
    {
        private static double[] Point3(double x, double y, double z)
        {
            return new[] { x, y, z };
        }

        private static double[] Point2(double x, double y)
        {
            return new[] { x, y };
        }

        private static void SaveScene(IZSceneDoc sceneDoc, string filePath)
        {
            try
            {
                sceneDoc.SaveAs(filePath, eZLinksSaveOptions.Z_LINKS_IGNORE, true);
            }
            catch
            {
                sceneDoc.SaveAs(filePath);
            }
        }

        private static IZPart CreateExtrudedProfilePart(IZSceneDoc sceneDoc, double[][] points, double depth)
        {
            IZProfile profile = sceneDoc.CreateProfile();
            if (profile == null)
                throw new InvalidOperationException("CreateProfile returned null.");

            for (int i = 0; i < points.Length; i++)
            {
                double[] start = points[i];
                double[] end = points[(i + 1) % points.Length];
                profile.CreateLine(Point2(start[0], start[1]), Point2(end[0], end[1]), i + 1);
            }

            IZPart part = sceneDoc.CreatePart();
            if (part == null)
                throw new InvalidOperationException("CreatePart returned null.");

            IZPartFeatureMgr features = (IZPartFeatureMgr)part;
            features.CreateExtrudeFeature(
                eZOperationType.Z_UNITE,
                false,
                depth,
                0.0,
                0.0,
                profile,
                eZFeatureProfileRelType.Z_FEATURE_PROFILE_ABSORB);

            try { part.Update(); } catch { }
            return part;
        }

        private static IZPart CreateNativePart(IZSceneDoc sceneDoc, int seq)
        {
            int recipe = (seq - 1) % 5;
            double n = seq;

            switch (recipe)
            {
                case 0:
                    {
                        double sx = 0.08 + (n * 0.015);
                        double sy = 0.05 + (n * 0.010);
                        double sz = 0.025 + (n * 0.008);
                        return sceneDoc.CreateBlockPart(
                            Point3(-sx / 2, -sy / 2, -sz / 2),
                            Point3(sx / 2, sy / 2, sz / 2));
                    }
                case 1:
                    {
                        double radius = 0.02 + (n * 0.004);
                        double height = 0.05 + (n * 0.010);
                        return sceneDoc.CreateCylinderPart(radius, height, Point3(0, 0, 0), Point3(0, 0, 1));
                    }
                case 2:
                    {
                        double radius = 0.025 + (n * 0.003);
                        return sceneDoc.CreateSpherePart(radius, Point3(0, 0, 0));
                    }
                case 3:
                    {
                        double radius = 0.03 + (n * 0.004);
                        double height = 0.06 + (n * 0.009);
                        double semiAngle = 0.30;
                        return sceneDoc.CreateConePart(radius, height, semiAngle, Point3(0, 0, 0), Point3(0, 0, 1));
                    }
                default:
                    {
                        double width = 0.10 + (n * 0.010);
                        double height = 0.08 + (n * 0.008);
                        double thick = 0.015 + (n * 0.003);
                        double[][] points =
                        {
                            new[] { -width / 2, -height / 2 },
                            new[] {  width / 2, -height / 2 },
                            new[] {  width / 2, -height / 2 + thick },
                            new[] { -width / 2 + thick, -height / 2 + thick },
                            new[] { -width / 2 + thick,  height / 2 },
                            new[] { -width / 2,  height / 2 }
                        };
                        return CreateExtrudedProfilePart(sceneDoc, points, thick * 2.5);
                    }
            }
        }

        [STAThread]
        private static int Main(string[] args)
        {
            string outputDir = args.Length > 0
                ? args[0]
                : @"C:\Users\TD-999\Research\ArasInnovator\copilot-worktrees\StudyCase_0603\IRONCASE";

            Directory.CreateDirectory(outputDir);

            string[] partNames =
            {
                "BasePlate",
                "SidePanel",
                "MotorMount",
                "Gearbox",
                "BeltPulley",
                "PCBBracket",
                "WiringDuct",
                "ValveBlock",
                "Cylinder",
                "HoseConnector",
                "SensorMount",
                "CoverLid"
            };

            Console.Write("Starting IronCAD...");
            Type icType = Type.GetTypeFromProgID("IronCAD.Application");
            if (icType == null)
            {
                Console.WriteLine(" FAILED.");
                Console.WriteLine("IronCAD.Application COM ProgID was not found.");
                return 1;
            }

            dynamic icApp = Activator.CreateInstance(icType);
            icApp.Visible = false;
            Console.WriteLine(" OK.");

            try
            {
                for (int i = 0; i < partNames.Length; i++)
                {
                    int seq = i + 1;
                    string icsFileName = $"IRONCASE_Ver1.0_{seq:D3}.ics";
                    string icsFilePath = Path.Combine(outputDir, icsFileName);

                    if (File.Exists(icsFilePath))
                    {
                        Console.WriteLine($"  {icsFileName} already exists, skipping.");
                        continue;
                    }

                    Console.Write($"  Creating {icsFileName} ({partNames[i]})...");

                    IZSceneDoc page = (IZSceneDoc)icApp.Pages.Add(Type.Missing, Type.Missing);
                    IZPart nativePart = CreateNativePart(page, seq);
                    if (nativePart == null)
                        throw new InvalidOperationException($"Native part creation returned null for {icsFileName}.");

                    try { ((IZElement)nativePart).Name = partNames[i]; } catch { }
                    try { nativePart.BOMPartNumber = $"IRONCASE_Ver1.0_{seq:D3}"; } catch { }
                    try { nativePart.BOMDescription = partNames[i]; } catch { }
                    try { nativePart.Update(); } catch { }
                    try { page.RegenerateParts(); } catch { }
                    try { page.Update(); } catch { }

                    SaveScene(page, icsFilePath);
                    try { ((dynamic)page).Close(); } catch { }
                    try { icApp.Pages.Remove(page); } catch { }

                    Console.WriteLine(" OK.");
                }

                string assemblyFileName = "Assembly-IRONCASE-Ver1.0A.ics";
                string assemblyPath = Path.Combine(outputDir, assemblyFileName);

                if (File.Exists(assemblyPath))
                {
                    Console.WriteLine($"  {assemblyFileName} already exists, skipping.");
                }
                else
                {
                    Console.Write($"  Creating {assemblyFileName}...");

                    dynamic asmPage = icApp.Pages.Add(Type.Missing, Type.Missing);

                    for (int i = 0; i < partNames.Length; i++)
                    {
                        int seq = i + 1;
                        string detailFileName = $"IRONCASE_Ver1.0_{seq:D3}.ics";
                        string detailPath = Path.Combine(outputDir, detailFileName);

                        if (!File.Exists(detailPath))
                        {
                            Console.WriteLine($"\n  WARNING: {detailFileName} not found, skipping.");
                            continue;
                        }

                        try
                        {
                            object added = asmPage.Shapes.Add(detailPath);
                            if (added is IZElement elem)
                            {
                                try { elem.Name = partNames[i]; } catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"\n  Shapes.Add failed for {detailFileName}: {ex.Message}");
                            try
                            {
                                asmPage.ImportFile(detailPath, true);
                            }
                            catch (Exception importEx)
                            {
                                Console.WriteLine($"\n  Link fallback failed for {detailFileName}: {importEx.Message}");
                            }
                        }
                    }

                    try { ((IZSceneDoc)asmPage).Update(); } catch { }
                    SaveScene((IZSceneDoc)asmPage, assemblyPath);
                    try { asmPage.Close(); } catch { }
                    try { icApp.Pages.Remove(asmPage); } catch { }

                    Console.WriteLine(" OK.");
                }

                Console.WriteLine("Done. All files created successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
            finally
            {
                try { icApp.Quit(); } catch { }
            }
        }
    }
}
