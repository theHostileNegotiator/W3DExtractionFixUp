using System;
using System.Collections;
using System.Text;
using System.Xml;

namespace W3DExtractionFixUp
{
    internal class Program
    {
        static int Errors = 0;

        static void EmitError(string e)
        {
            Console.WriteLine("Error: {0}", e);
            ++Errors;
        }

        static void W3DHierarchyFixup(XmlDocument doc)
        {
            XmlNodeList fixupMatrices = doc.GetElementsByTagName("FixupMatrix");
            foreach (XmlNode FixupMatrix in fixupMatrices)
            {
                XmlAttributeCollection FixupMatrixAttributes = FixupMatrix.Attributes;
                FixupMatrixAttributes.Remove(FixupMatrixAttributes["M03"]);
                FixupMatrixAttributes.Remove(FixupMatrixAttributes["M13"]);
                FixupMatrixAttributes.Remove(FixupMatrixAttributes["M23"]);
                FixupMatrixAttributes.Remove(FixupMatrixAttributes["M33"]);
            }

            XmlNodeList pivots = doc.GetElementsByTagName("Pivot");
            foreach (XmlNode pivot in pivots)
            {
                XmlNamedNodeMap pivotsAttributes = pivot.Attributes;
                // Hierarchy to all caps
                pivotsAttributes.Item(0).Value = pivotsAttributes.Item(0).Value.ToUpperInvariant();
            }

            // Remove Comments
            XmlNodeList Comments = doc.SelectNodes("//comment()");
            foreach (XmlNode Comment in Comments)
            {
                Comment.ParentNode.RemoveChild(Comment);
            }

        }

        static void W3DMeshFixup(XmlDocument doc)
        {
            XmlNodeList vertexColors = doc.GetElementsByTagName("VertexColors");
            if (vertexColors.Count > 0)
            {
                XmlNodeList cElements = vertexColors[0].ChildNodes;
                bool HasVertexColor = false;

                foreach (XmlNode cElement in cElements)
                {
                    XmlAttributeCollection rgbaAttributes = cElement.Attributes;
                    foreach (XmlNode rgbaAttribute in rgbaAttributes)
                    {
                        double ColorChannelValue = double.Parse(rgbaAttribute.Value);
                        if (ColorChannelValue < 1.0)
                        {
                            HasVertexColor = true;
                            // Fix Rounding
                            if (ColorChannelValue > 0.0)
                            {
                                double ColorToByteRound = Math.Round(ColorChannelValue * 255.0);
                                // Should always round up as when compiling, converts to Byte and removes the decimal, always rounds down
                                ColorChannelValue = Math.Ceiling((ColorToByteRound / 255.0) * 1000000) * 0.000001;
                                rgbaAttribute.Value = String.Format("{0:0.000000}", ColorChannelValue);
                            }
                        }
                    }
                }
                if (!HasVertexColor)
                {
                    vertexColors[0].ParentNode.RemoveChild(vertexColors[0]);
                }
            }
            XmlNodeList fxShader = doc.GetElementsByTagName("FXShader");
            if (fxShader.Count > 0)
            {
                XmlAttributeCollection fxShaderAttributes = fxShader[0].Attributes;
                bool HasTechnique = false;
                foreach (XmlNode fxShaderAttribute in fxShaderAttributes)
                {
                    if (fxShaderAttribute.Name == "TechniqueIndex")
                    {
                        HasTechnique = true;
                    }
                }
                if (!HasTechnique)
                {
                    XmlAttribute ShaderTechnique = doc.CreateAttribute("TechniqueIndex");
                    ShaderTechnique.Value = "0";
                    fxShaderAttributes.Append(ShaderTechnique);
                }
            }
        }

        static void W3DMeshIncludes(XmlDocument doc, XmlDocument docRnd, ArrayList texturesList)
        {
            XmlNodeList textureList = docRnd.GetElementsByTagName("Texture");
            foreach (XmlNode texture in textureList)
            {
                string textureId = texture.InnerText;
                textureId = textureId.Trim();
                if (!texturesList.Contains(textureId.ToLower()))
                {
                    XmlElement include = doc.CreateElement("Include", "uri:ea.com:eala:asset");
                    include.SetAttribute("type", "all");
                    include.SetAttribute("source", $"ART:{textureId}.xml");
                    doc.AppendChild(include);

                    texturesList.Add(textureId.ToLower());
                }
            }
        }

        static void FirstPassContainer(string dumpedFolder, List<string> fileList)
        {
            // DirectoryInfo directory = new DirectoryInfo(dumpedFolder);
            string[] w3xFiles = Directory.GetFiles(dumpedFolder, "*.w3x");
            string lowLodDirectory = dumpedFolder + Path.DirectorySeparatorChar + "LowLOD" + Path.DirectorySeparatorChar;
            string medLodDirectory = dumpedFolder + Path.DirectorySeparatorChar + "MedLOD" + Path.DirectorySeparatorChar;

            // Check if it's a container file
            foreach (string w3xFile in w3xFiles)
            {
                // May be deleted as we process Containers on this pass
                if (!File.Exists(w3xFile))
                {
                    continue;
                }
                // XmlTextReader reader = new XmlTextReader(w3xFile);
                XmlDocument docSkn = new XmlDocument();

                // Check Low and MediumLOD Container
                string file = w3xFile;
                // 0, no LOD, 1 is LOD, 2 LOD is done
                int isLowLOD = 0;
                int isMedLOD = 0;
                bool noLowLOD = false;

                for (int i = 0; i <= 2 && !noLowLOD; i++)
                {

                    if (File.Exists(lowLodDirectory + Path.GetFileName(w3xFile)) && isLowLOD != 2)
                    {
                        file = lowLodDirectory + Path.GetFileName(w3xFile);
                        docSkn.Load(file);
                        isLowLOD = 1;
                    }
                    else if (File.Exists(medLodDirectory + Path.GetFileName(w3xFile)) && isMedLOD != 2)
                    {
                        file = medLodDirectory + Path.GetFileName(w3xFile);
                        docSkn.Load(file);
                        isMedLOD = 1;
                    }
                    else
                    {
                        file = w3xFile;
                        docSkn.Load(file);
                    }
                    XmlNodeList w3dContainers = docSkn.GetElementsByTagName("W3DContainer");
                    if (w3dContainers.Count == 0)
                    {
                        noLowLOD = true;
                        continue;
                    }

                    fileList.Add(file);

                    XmlDocument doc = new XmlDocument();
                    XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
                    XmlElement rootNode = doc.CreateElement("AssetDeclaration", "uri:ea.com:eala:asset");
                    XmlAttribute nsDeclaration = doc.CreateAttribute("xmlns");
                    nsDeclaration.Value = "uri:ea.com:eala:asset";
                    rootNode.Attributes.Append(nsDeclaration);
                    XmlAttribute xsiDeclaration = doc.CreateAttribute("xmlns:xsi");
                    xsiDeclaration.Value = "http://www.w3.org/2001/XMLSchema-instance";
                    rootNode.Attributes.Append(xsiDeclaration);

                    XmlElement includes = doc.CreateElement("Includes", "uri:ea.com:eala:asset");

                    string containerId = w3dContainers[0].Attributes["id"]?.Value.ToUpperInvariant();
                    string hierarchyId = w3dContainers[0].Attributes["Hierarchy"]?.Value.ToUpperInvariant();

                    XmlNamedNodeMap w3dContainerAttributes = w3dContainers[0].Attributes;
                    // Hierarchy to all caps
                    w3dContainerAttributes.Item(1).Value = hierarchyId;

                    XmlNode impContainer = doc.ImportNode(w3dContainers[0], true);
                    rootNode.AppendChild(impContainer);

                    Console.WriteLine("Processing Container: " + containerId);

                    // Process Container
                    // Check if Hierarchy has same ID
                    if (containerId == hierarchyId)
                    {
                        string hierarchyFile = dumpedFolder + Path.DirectorySeparatorChar + containerId + "_HRC.w3x";
                        if (File.Exists(lowLodDirectory + containerId + "_HRC.w3x") && isMedLOD != 1 && isLowLOD != 2)
                        {
                            hierarchyFile = lowLodDirectory + containerId + "_HRC.w3x";
                            isLowLOD = 1;
                        }
                        else if (File.Exists(medLodDirectory + containerId + "_HRC.w3x") && isLowLOD != 1 && isMedLOD != 2)
                        {
                            hierarchyFile = medLodDirectory + containerId + "_HRC.w3x";
                            isMedLOD = 1;
                        }
                        if (!fileList.Contains(hierarchyFile))
                        {
                            fileList.Add(hierarchyFile);
                        }
                        Console.WriteLine("\tAdding Hierarchy: " + hierarchyId);
                        // If True, import Hierarchy in same file
                        XmlDocument docSkl = new XmlDocument();
                        // Bibbers Suffix for Hierarchy _HRC
                        docSkl.Load(hierarchyFile);
                        W3DHierarchyFixup(docSkl);
                        XmlNodeList w3dHierarchy = docSkl.GetElementsByTagName("W3DHierarchy");
                        XmlNode impHierarchy = doc.ImportNode(w3dHierarchy[0], true);
                        rootNode.PrependChild(impHierarchy);
                    }
                    else
                    {
                        XmlElement include = doc.CreateElement("Include", "uri:ea.com:eala:asset");
                        include.SetAttribute("type", "all");
                        include.SetAttribute("source", $"ART:{hierarchyId.ToLower()}.w3x");
                        includes.AppendChild(include);
                    }

                    // Then Check if Animation has same ID
                    string animationFile = null;
                    if (File.Exists(lowLodDirectory + containerId + ".w3x") && isMedLOD != 1 && isLowLOD != 2)
                    {
                        animationFile = lowLodDirectory + containerId + ".w3x";
                        isLowLOD = 1;
                    }
                    else if (File.Exists(medLodDirectory + containerId + ".w3x") && isLowLOD != 1 && isMedLOD != 2)
                    {
                        animationFile = medLodDirectory + containerId + ".w3x";
                        isMedLOD = 1;
                    }
                    else
                    {
                        animationFile = dumpedFolder + Path.DirectorySeparatorChar + containerId + ".w3x";
                    }
                    if (File.Exists(animationFile) && animationFile != w3xFile)
                    {
                        if (!fileList.Contains(animationFile))
                        {
                            fileList.Add(animationFile);
                        }
                        Console.WriteLine("\tAdding Animation: " + containerId);
                        XmlDocument docAnm = new XmlDocument();
                        docAnm.Load(animationFile);
                        XmlNodeList w3dAnimation = docAnm.GetElementsByTagName("W3DAnimation");
                        if (w3dAnimation.Count > 0)
                        {
                            XmlNamedNodeMap w3dAnimationAttributes = w3dAnimation[0].Attributes;
                            // Item(1) = Hierarchy
                            w3dAnimationAttributes.Item(1).Value = hierarchyId;
                            XmlNode impAnimation = doc.ImportNode(w3dAnimation[0], true);
                            rootNode.InsertBefore(impAnimation, impContainer);
                        }
                    }
                    // Go through subObjects
                    XmlNodeList subObjects = docSkn.GetElementsByTagName("SubObject");
                    if (subObjects.Count > 0)
                    {
                        ArrayList texturesList = new ArrayList();
                        foreach (XmlNode subObject in subObjects)
                        {
                            // string strValue = subObject.FirstChild.FirstChild.InnerText;
                            string renderObject = subObject.FirstChild.FirstChild.InnerText;
                            string renderFile = "";
                            if (File.Exists(lowLodDirectory + renderObject + ".w3x") && isMedLOD != 1 && isLowLOD != 2)
                            {
                                renderFile = lowLodDirectory + renderObject + ".w3x";
                                isLowLOD = 1;
                            }
                            else if (File.Exists(medLodDirectory + renderObject + ".w3x") && isLowLOD != 1 && isMedLOD != 2)
                            {
                                renderFile = medLodDirectory + renderObject + ".w3x";
                                isMedLOD = 1;
                            }
                            else
                            {
                                renderFile = dumpedFolder + Path.DirectorySeparatorChar + renderObject + ".w3x";
                            }
                            if (File.Exists(renderFile))
                            {
                                if (!fileList.Contains(renderFile))
                                {
                                    fileList.Add(renderFile);
                                }
                                XmlDocument docRnd = new XmlDocument();
                                docRnd.Load(renderFile);
                                XmlNodeList w3dMesh = docRnd.GetElementsByTagName("W3DMesh");
                                XmlNodeList w3dCollisionBox = docRnd.GetElementsByTagName("W3DCollisionBox");
                                XmlNode impRender = null;
                                if (w3dMesh.Count > 0)
                                {
                                    Console.WriteLine("\tAdding Mesh: " + renderObject);
                                    W3DMeshFixup(docRnd);
                                    impRender = doc.ImportNode(w3dMesh[0], true);
                                    // Add textures to includes
                                    //W3DMeshIncludes(doc, docRnd, texturesList);
                                    XmlNodeList textureList = docRnd.GetElementsByTagName("Texture");
                                    foreach (XmlNode texture in textureList)
                                    {
                                        string textureId = texture.InnerText;
                                        textureId = textureId.Trim();
                                        if (!texturesList.Contains(textureId.ToLower()))
                                        {
                                            XmlElement include = doc.CreateElement("Include", "uri:ea.com:eala:asset");
                                            include.SetAttribute("type", "all");
                                            include.SetAttribute("source", $"ART:{textureId}.xml");
                                            includes.AppendChild(include);

                                            texturesList.Add(textureId.ToLower());
                                        }
                                    }
                                }
                                else if (w3dCollisionBox.Count > 0)
                                {
                                    Console.WriteLine("\tAdding Collision Box: " + renderObject);
                                    // Remove JoypadPicking Attribute if set to default value
                                    XmlAttributeCollection w3dCollisionAttributes = w3dCollisionBox[0].Attributes;
                                    bool isJoypadPickingDefault = false;
                                    XmlNamedNodeMap AttributeList = w3dCollisionBox[0].Attributes;
                                    foreach (XmlNode w3dCollisionAttribute in w3dCollisionAttributes)
                                    {
                                        if (w3dCollisionAttribute.Name == "JoypadPickingOnly")
                                        {
                                            if (w3dCollisionAttribute.Value == "false")
                                            {
                                                isJoypadPickingDefault = true;
                                            }
                                        }
                                    }
                                    if (isJoypadPickingDefault)
                                    {
                                        w3dCollisionAttributes.Remove(w3dCollisionAttributes["JoypadPickingOnly"]);
                                    }

                                    impRender = doc.ImportNode(w3dCollisionBox[0], true);
                                }
                                rootNode.InsertBefore(impRender, impContainer);
                            }
                        }
                    }
                    Console.WriteLine("\tGenerating Includes");
                    rootNode.PrependChild(includes);
                    // Includes
                    doc.AppendChild(rootNode);

                    StringBuilder builder = new StringBuilder();
                    XmlWriterSettings settings = new XmlWriterSettings();

                    XmlWriter xmlWriter = XmlWriter.Create(builder,
                        new XmlWriterSettings()
                    {
                        CloseOutput = true,
                        Indent = true,
                        IndentChars = "\t"
                    });

                    using (xmlWriter)
                    {
                        doc.Save(xmlWriter);
                    }
                
                    builder.Replace(" />", "/>");
                    builder.Replace("utf-16", "UTF-8");
                    // Not have self closing empty elements
                    builder.Replace("<Includes/>", "<Includes></Includes>");
                    builder.Replace("<Channels/>", "<Channels></Channels>");
                    // Container is always the last element before closing AssetDeclaration. This is at least predictable without needing to use Regex
                    builder.Replace("/>\r\n</AssetDeclaration>", "></W3DContainer>\r\n</AssetDeclaration>");

                    DirectoryInfo compiledSubFolder = new DirectoryInfo(Path.Combine(dumpedFolder, "Compiled"));
                    compiledSubFolder.CreateSubdirectory(containerId.Substring(0, 2));
                    string postFix = "";
                    if (isLowLOD == 1)
                    {
                        postFix = "_L";
                        isLowLOD = 2;
                    }
                    else if (isMedLOD == 1)
                    {
                        postFix = "_M";
                        isMedLOD = 2;
                    }
                    else
                    {
                        noLowLOD = true;
                    }

                    string saveFile = dumpedFolder + Path.DirectorySeparatorChar + "Compiled" + Path.DirectorySeparatorChar + containerId.Substring(0, 2) + Path.DirectorySeparatorChar + containerId + postFix + ".w3x";
                    File.WriteAllBytes(saveFile, Encoding.UTF8.GetBytes(builder.ToString()));
                    Console.WriteLine("\tFile saved to: " + saveFile);
                }
            }
        }

        static void SecondPassW3X(string dumpedFolder, List<string> fileList)
        {
            DirectoryInfo directory = new DirectoryInfo(dumpedFolder);
            string[] w3xFilesHighLOD = Directory.GetFiles(dumpedFolder, "*.w3x");
            string[] w3xFilesLowLOD = Directory.Exists(Path.Combine(dumpedFolder, "LowLOD")) ? Directory.GetFiles(Path.Combine(dumpedFolder, "LowLOD"), "*.w3x") : Array.Empty<string>();
            string[] w3xFilesMedLOD = Directory.Exists(Path.Combine(dumpedFolder, "MedLOD")) ? Directory.GetFiles(Path.Combine(dumpedFolder, "MedLOD"), "*.w3x") : Array.Empty<string>();
            string[] w3xFiles = w3xFilesHighLOD.Concat(w3xFilesLowLOD).Concat(w3xFilesMedLOD).ToArray();

            foreach (string w3xFile in w3xFiles)
            {
                if (fileList.Contains(w3xFile))
                {
                    continue;
                }
                XmlDocument docW3X = new XmlDocument();
                docW3X.Load(w3xFile);
                XmlNodeList w3dHierarchy = docW3X.GetElementsByTagName("W3DHierarchy");
                XmlNodeList w3dAnimation = docW3X.GetElementsByTagName("W3DAnimation");
                XmlNodeList w3dMesh = docW3X.GetElementsByTagName("W3DMesh");

                if (w3dHierarchy.Count == 0 && w3dAnimation.Count == 0 && w3dMesh.Count == 0)
                {
                    continue;
                }

                XmlDocument doc = new XmlDocument();
                XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
                XmlElement rootNode = doc.CreateElement("AssetDeclaration", "uri:ea.com:eala:asset");
                XmlAttribute nsDeclaration = doc.CreateAttribute("xmlns");
                nsDeclaration.Value = "uri:ea.com:eala:asset";
                rootNode.Attributes.Append(nsDeclaration);
                XmlAttribute xsiDeclaration = doc.CreateAttribute("xmlns:xsi");
                xsiDeclaration.Value = "http://www.w3.org/2001/XMLSchema-instance";
                rootNode.Attributes.Append(xsiDeclaration);

                XmlElement includes = doc.CreateElement("Includes", "uri:ea.com:eala:asset");

                string w3dId = "";

                if (w3dHierarchy.Count > 0)
                {
                    w3dId = w3dHierarchy[0].Attributes["id"]?.Value;
                    Console.WriteLine("Processing Hierarchy: " + w3dId);
                    W3DHierarchyFixup(docW3X);
                    XmlNode impHierarchy = doc.ImportNode(w3dHierarchy[0], true);
                    rootNode.AppendChild(impHierarchy);
                    rootNode.PrependChild(includes);
                    doc.AppendChild(rootNode);
                }

                if (w3dAnimation.Count > 0)
                {
                    w3dId = w3dAnimation[0].Attributes["id"]?.Value;
                    Console.WriteLine("Processing Animation: " + w3dId);

                    XmlNamedNodeMap w3dAnimationAttributes = w3dAnimation[0].Attributes;
                    // Item(1) = Hierarchy
                    w3dAnimationAttributes.Item(1).Value = w3dAnimationAttributes.Item(1).Value.ToUpperInvariant();

                    XmlNode impAnimation = doc.ImportNode(w3dAnimation[0], true);
                    rootNode.AppendChild(impAnimation);

                    Console.WriteLine("\tGenerating Includes");
                    XmlElement include = doc.CreateElement("Include", "uri:ea.com:eala:asset");
                    include.SetAttribute("type", "all");
                    include.SetAttribute("source", $"ART:{w3dAnimation[0].Attributes["Hierarchy"]?.Value.ToLower()}.w3x");
                    includes.AppendChild(include);

                    rootNode.PrependChild(includes);
                    doc.AppendChild(rootNode);
                }

                if (w3dMesh.Count > 0)
                {
                    w3dId = w3dMesh[0].Attributes["id"]?.Value;
                    Console.WriteLine("Processing Mesh: " + w3dId);
                    W3DMeshFixup(docW3X);
                    XmlNode impRender = doc.ImportNode(w3dMesh[0], true);

                    Console.WriteLine("\tGenerating Includes");
                    ArrayList texturesList = new ArrayList();
                    //W3DMeshIncludes(doc, docW3X, texturesList);
                    
                    XmlNodeList textureList = docW3X.GetElementsByTagName("Texture");
                    foreach (XmlNode texture in textureList)
                    {
                        string textureId = texture.InnerText;
                        textureId = textureId.Trim();
                        if (!texturesList.Contains(textureId.ToLower()))
                        {
                            XmlElement include = doc.CreateElement("Include", "uri:ea.com:eala:asset");
                            include.SetAttribute("type", "all");
                            include.SetAttribute("source", $"ART:{textureId}.xml");
                            includes.AppendChild(include);

                            texturesList.Add(textureId.ToLower());
                        }
                    }
                    
                    rootNode.AppendChild(impRender);
                    rootNode.PrependChild(includes);
                    doc.AppendChild(rootNode);
                }

                StringBuilder builder = new StringBuilder();
                XmlWriterSettings settings = new XmlWriterSettings();

                XmlWriter xmlWriter = XmlWriter.Create(builder,
                    new XmlWriterSettings()
                    {
                        CloseOutput = true,
                        Indent = true,
                        IndentChars = "\t"
                    });

                using (xmlWriter)
                {
                    doc.Save(xmlWriter);
                }

                builder.Replace(" />", "/>");
                builder.Replace("utf-16", "UTF-8");
                // Not have self closing elements
                builder.Replace("<Includes/>", "<Includes></Includes>");
                builder.Replace("<Channels/>", "<Channels></Channels>");

                DirectoryInfo compiledSubFolder = new DirectoryInfo(Path.Combine(dumpedFolder, "Compiled"));
                compiledSubFolder.CreateSubdirectory(w3dId.Substring(0, 2));
                string postFix = "";
                if (w3xFile.Contains(Path.DirectorySeparatorChar + "LowLOD"))
                {
                    postFix = "_L";
                }
                else if (w3xFile.Contains(Path.DirectorySeparatorChar + "MedLOD"))
                {
                    postFix = "_M";
                }

                string saveFile = dumpedFolder + Path.DirectorySeparatorChar + "Compiled" + Path.DirectorySeparatorChar + w3dId.Substring(0, 2) + Path.DirectorySeparatorChar + w3dId + postFix + ".w3x";
                File.WriteAllBytes(saveFile, Encoding.UTF8.GetBytes(builder.ToString()));
                Console.WriteLine("\tFile saved to: " + saveFile);
            }
        }

        static void MoveTextures(string dumpedFolder)
        {
            DirectoryInfo directory = new DirectoryInfo(dumpedFolder);
            string[] ddsFilesHighLOD = Directory.GetFiles(dumpedFolder, "*.dds");
            string[] ddsFilesLowLOD = Directory.Exists(Path.Combine(dumpedFolder, "LowLOD")) ? Directory.GetFiles(Path.Combine(dumpedFolder, "LowLOD"), "*.dds") : Array.Empty<string>();
            string[] ddsFilesMedLOD = Directory.Exists(Path.Combine(dumpedFolder, "MedLOD")) ? Directory.GetFiles(Path.Combine(dumpedFolder, "MedLOD"), "*.dds") : Array.Empty<string>();
            string[] ddsFiles = ddsFilesHighLOD.Concat(ddsFilesLowLOD).Concat(ddsFilesMedLOD).ToArray();

            string artImagesFolder = dumpedFolder + Path.DirectorySeparatorChar + "Compiled" + Path.DirectorySeparatorChar + "Images";
            // A decent auto check if array has RA3+ specific content
            bool isRA3Plus = ddsFiles.Any(file => file.StartsWith(Path.Combine(dumpedFolder, "ShellPackedImages")));
            if (isRA3Plus)
            {
                // RA3+ images/pc/
                artImagesFolder = Path.Combine(artImagesFolder, "pc");
            }

            foreach (string ddsFile in ddsFiles)
            {
                string postFix = "";
                if (ddsFile.Contains(Path.DirectorySeparatorChar + "LowLOD"))
                {
                    postFix = "_L";
                }
                else if (ddsFile.Contains(Path.DirectorySeparatorChar + "MedLOD"))
                {
                    postFix = "_M";
                }

                Console.WriteLine("Moving Texture File: " + ddsFile);
                string textureId = Path.GetFileNameWithoutExtension(ddsFile);

                DirectoryInfo compiledSubFolder = new DirectoryInfo(Path.Combine(dumpedFolder, "Compiled"));
                if (textureId.StartsWith("PackedImages") || textureId.StartsWith("PackedLocalisedImages") || textureId.StartsWith("IndividualImages") || textureId.StartsWith("ShellPackedImages"))
                {
                    Console.WriteLine("\tTo: " + artImagesFolder + Path.DirectorySeparatorChar + textureId + postFix + ".dds");
                    compiledSubFolder.CreateSubdirectory("Images");
                    if (isRA3Plus)
                    {
                        DirectoryInfo compiledImageFolder = new DirectoryInfo(Path.Combine(Path.Combine(dumpedFolder, "Compiled"), "Images"));
                        compiledImageFolder.CreateSubdirectory("pc");
                    }
                    File.Copy(ddsFile, artImagesFolder + Path.DirectorySeparatorChar + textureId + postFix + ".dds", true);
                }
                else
                {
                    compiledSubFolder.CreateSubdirectory(textureId.Substring(0, 2).ToUpperInvariant());
                    Console.WriteLine("\tTo: " + dumpedFolder + Path.DirectorySeparatorChar + "Compiled" + Path.DirectorySeparatorChar + textureId.Substring(0, 2).ToUpperInvariant() + Path.DirectorySeparatorChar + textureId + postFix + ".dds");
                    File.Copy(ddsFile, dumpedFolder + Path.DirectorySeparatorChar + "Compiled" + Path.DirectorySeparatorChar + textureId.Substring(0, 2).ToUpperInvariant() + Path.DirectorySeparatorChar + textureId + postFix + ".dds", true);
                    File.Copy(dumpedFolder + Path.DirectorySeparatorChar + textureId + ".xml", dumpedFolder + Path.DirectorySeparatorChar + "Compiled" + Path.DirectorySeparatorChar + textureId.Substring(0, 2) + Path.DirectorySeparatorChar + textureId + postFix + ".xml", true);
                }
            }
        }

        static void Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    Console.WriteLine("Usage: W3DExtractionFixUp input_path");
                    Console.WriteLine("");
                    Console.WriteLine("  input_path\t\tDirectory where the Extracted Art Files are Dumped");
                }
                else
                {
                    string rootPath = Path.Combine(Path.GetDirectoryName(args[0]), Path.GetFileName(args[0]));
                    List<string> filesProcessed = new List<string>();
                    // Scan all files once for Containers
                    FirstPassContainer(rootPath, filesProcessed);
                    // Move W3X Files, sort and rename
                    SecondPassW3X(rootPath, filesProcessed);
                    // Move Texture Files
                    MoveTextures(rootPath);
                }
            }
            catch (Exception e)
            {
                EmitError("Failed with exception: {0}" + Convert.ToString(e));
            }
        }
    }
}
