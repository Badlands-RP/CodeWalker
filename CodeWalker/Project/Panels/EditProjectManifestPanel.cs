using CodeWalker.GameFiles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using WeifenLuo.WinFormsUI.Docking;

namespace CodeWalker.Project.Panels
{
    public partial class EditProjectManifestPanel : ProjectPanel
    {
        public ProjectForm ProjectForm { get; set; }
        public ProjectFile CurrentProjectFile { get; set; }

        public EditProjectManifestPanel(ProjectForm projectForm)
        {
            ProjectForm = projectForm;
            InitializeComponent();
            Tag = "_manifest.ymf";
        }

        public override void SetTheme(ThemeBase theme)
        {
            base.SetTheme(theme);

            var txtback = SystemColors.Window;
            var indback = Color.WhiteSmoke;

            if (theme is VS2015DarkTheme)
            {
                txtback = theme.ColorPalette.MainWindowActive.Background;
                indback = theme.ColorPalette.MainWindowActive.Background;
            }

            ProjectManifestTextBox.BackColor = txtback;
            ProjectManifestTextBox.IndentBackColor = indback;

        }


        public void SetProject(ProjectFile project)
        {
            //TODO: include _manifest.ymf in project and load/save

            CurrentProjectFile = project;

            GenerateProjectManifest();
        }




        private void GenerateProjectManifest()
        {
            ProjectManifestTextBox.Text = GenerateProjectManifestXml();
            Text = "_manifest.ymf*";
        }

        private string GenerateProjectManifestXml()
        {
            EnsureProjectManifestHashNames();

            var sb = new StringBuilder();
            var mapdeps = new Dictionary<string, YtypFile>();
            var typdeps = new Dictionary<string, Dictionary<string, YtypFile>>();
            var interiors = new List<string>();

            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>");
            sb.AppendLine("<CPackFileMetaData>");
            sb.AppendLine("  <MapDataGroups/>");
            sb.AppendLine("  <HDTxdBindingArray/>");
            sb.AppendLine("  <imapDependencies/>");


            var getYtypName = new Func<YtypFile, string>((ytyp) =>
            {
                var ytypname = ytyp?.RpfFileEntry?.NameLower;
                if (ytyp != null)
                {
                    if (string.IsNullOrEmpty(ytypname))
                    {
                        ytypname = ytyp.RpfFileEntry?.Name?.ToLowerInvariant();
                        if (ytypname == null) ytypname = "";
                    }
                    if (ytypname.EndsWith(".ytyp"))
                    {
                        ytypname = ytypname.Substring(0, ytypname.Length - 5);
                    }
                }
                return ytypname;
            });


            if (CurrentProjectFile != null)
            {
                if (CurrentProjectFile.YmapFiles.Count > 0)
                {
                    sb.AppendLine("  <imapDependencies_2>");
                    foreach (var ymap in CurrentProjectFile.YmapFiles)
                    {
                        var ymapname = ymap.RpfFileEntry?.NameLower;
                        if (string.IsNullOrEmpty(ymapname))
                        {
                            ymapname = ymap.Name.ToLowerInvariant();
                        }
                        if (ymapname.EndsWith(".ymap"))
                        {
                            ymapname = ymapname.Substring(0, ymapname.Length - 5);
                        }

                        mapdeps.Clear();
                        bool ismilo = false;
                        if (ymap.AllEntities != null)
                        {
                            foreach (var ent in ymap.AllEntities)
                            {
                                var ytyp = ent.Archetype?.Ytyp;
                                var ytypname = getYtypName(ytyp);
                                if (ytyp != null)
                                {
                                    mapdeps[ytypname] = ytyp;
                                }

                                if (ent.IsMlo)
                                {
                                    ismilo = true;
                                    if (ent.MloInstance?.Entities != null)
                                    {
                                        Dictionary<string, YtypFile> typdepdict;
                                        if (!typdeps.TryGetValue(ytypname, out typdepdict))
                                        {
                                            typdepdict = new Dictionary<string, YtypFile>();
                                            typdeps[ytypname] = typdepdict;
                                        }
                                        foreach (var ient in ent.MloInstance.Entities)
                                        {
                                            var iytyp = ient.Archetype?.Ytyp;
                                            var iytypname = getYtypName(iytyp);
                                            if ((iytyp != null) && (iytypname != ytypname))
                                            {
                                                typdepdict[iytypname] = iytyp;
                                            }
                                        }
                                    }
                                }

                            }
                        }
                        if (ymap.GrassInstanceBatches != null)
                        {
                            foreach (var batch in ymap.GrassInstanceBatches)
                            {
                                var ytyp = batch.Archetype?.Ytyp;
                                var ytypname = getYtypName(ytyp);
                                if (ytyp != null)
                                {
                                    mapdeps[ytypname] = ytyp;
                                }
                            }
                        }

                        sb.AppendLine("    <Item>");
                        sb.AppendLine("      <imapName>" + ymapname + "</imapName>");
                        if (ismilo)
                        {
                            sb.AppendLine("      <manifestFlags>INTERIOR_DATA</manifestFlags>");
                        }
                        else
                        {
                            sb.AppendLine("      <manifestFlags/>");
                        }
                        sb.AppendLine("      <itypDepArray>");
                        foreach (var kvp in mapdeps)
                        {
                            sb.AppendLine("        <Item>" + kvp.Key + "</Item>");
                        }
                        sb.AppendLine("      </itypDepArray>");
                        sb.AppendLine("    </Item>");
                    }
                    sb.AppendLine("  </imapDependencies_2>");
                }
                else
                {
                    sb.AppendLine("  <imapDependencies_2/>");
                }

                if ((CurrentProjectFile.YtypFiles.Count > 0) && (ProjectForm?.GameFileCache != null))
                {
                    foreach (var ytyp in CurrentProjectFile.YtypFiles)
                    {
                        var ytypname = getYtypName(ytyp);
                        foreach (var archm in ytyp.AllArchetypes)
                        {
                            var mloa = archm as MloArchetype;
                            if (mloa != null)
                            {
                                interiors.Add(mloa.Name);
                                Dictionary<string, YtypFile> typdepdict;
                                if (!typdeps.TryGetValue(ytypname, out typdepdict))
                                {
                                    typdepdict = new Dictionary<string, YtypFile>();
                                    typdeps[ytypname] = typdepdict;
                                }
                                if (mloa.entities != null)
                                {
                                    foreach (var ent in mloa.entities)
                                    {
                                        var archname = ent._Data.archetypeName;
                                        var arch = ProjectForm.GameFileCache.GetArchetype(archname);
                                        var iytyp = arch?.Ytyp;
                                        var iytypname = getYtypName(iytyp);
                                        if ((iytyp != null) && (iytypname != ytypname))
                                        {
                                            typdepdict[iytypname] = iytyp;
                                        }
                                    }
                                }
                                if (mloa.entitySets != null)
                                {
                                    foreach (var entset in mloa.entitySets)
                                    {
                                        if (entset.Entities != null)
                                        {
                                            foreach (var ent in entset.Entities)
                                            {
                                                var archname = ent._Data.archetypeName;
                                                var arch = ProjectForm.GameFileCache.GetArchetype(archname);
                                                var iytyp = arch?.Ytyp;
                                                var iytypname = getYtypName(iytyp);
                                                if ((iytyp != null) && (iytypname != ytypname))
                                                {
                                                    typdepdict[iytypname] = iytyp;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

            }

            if (typdeps.Count > 0)
            {
                sb.AppendLine("  <itypDependencies_2>");
                foreach (var kvp1 in typdeps)
                {
                    sb.AppendLine("    <Item>");
                    sb.AppendLine("      <itypName>" + kvp1.Key + "</itypName>");
                    sb.AppendLine("      <manifestFlags>INTERIOR_DATA</manifestFlags>");
                    sb.AppendLine("      <itypDepArray>");
                    foreach (var kvp2 in kvp1.Value)
                    {
                        sb.AppendLine("        <Item>" + kvp2.Key + "</Item>");
                    }
                    sb.AppendLine("      </itypDepArray>");
                    sb.AppendLine("    </Item>");
                }
                sb.AppendLine("  </itypDependencies_2>");
            }
            else
            {
                sb.AppendLine("  <itypDependencies_2/>");
            }

            if (interiors.Count > 0)
            {
                sb.AppendLine("  <Interiors itemType=\"CInteriorBoundsFiles\">");
                foreach (var interior in interiors)
                {
                    sb.AppendLine("    <Item>");
                    sb.AppendLine("      <Name>" + interior + "</Name>");
                    sb.AppendLine("      <Bounds>");
                    sb.AppendLine("        <Item>" + interior + "</Item>");
                    sb.AppendLine("      </Bounds>");
                    sb.AppendLine("    </Item>");
                }
                sb.AppendLine("  </Interiors>");
            }
            else
            {
                sb.AppendLine("  <Interiors/>");
            }
            sb.AppendLine("</CPackFileMetaData>");

            return sb.ToString();
        }

        private void ProjectManifestGenerateButton_Click(object sender, EventArgs e)
        {
            CurrentProjectFile = ProjectForm.CurrentProjectFile;
            GenerateProjectManifest();
        }

        private void LoadManifestButton_Click(object sender, EventArgs e)
        {
            if (OpenFileDialog.ShowDialog() != DialogResult.OK) return;

            try
            {
                CurrentProjectFile = ProjectForm.CurrentProjectFile;

                var filename = OpenFileDialog.FileName;
                ProjectManifestTextBox.Text = LoadManifestXml(filename);
                Text = new FileInfo(filename).Name + "*";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading _manifest.ymf file:\n" + ex.ToString());
            }
        }

        private void UpdateManifestButton_Click(object sender, EventArgs e)
        {
            try
            {
                CurrentProjectFile = ProjectForm.CurrentProjectFile;

                if (string.IsNullOrWhiteSpace(ProjectManifestTextBox.Text))
                {
                    GenerateProjectManifest();
                    return;
                }

                var currentDoc = new XmlDocument();
                currentDoc.LoadXml(ProjectManifestTextBox.Text);

                var generatedDoc = new XmlDocument();
                generatedDoc.LoadXml(GenerateProjectManifestXml());

                MergeManifestSection(currentDoc, generatedDoc, "imapDependencies_2", "imapName");
                MergeManifestSection(currentDoc, generatedDoc, "itypDependencies_2", "itypName");
                MergeManifestSection(currentDoc, generatedDoc, "Interiors", "Name");

                ProjectManifestTextBox.Text = FormatXml(currentDoc);
                Text = "_manifest.ymf*";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error updating _manifest.ymf XML:\n" + ex.ToString());
            }
        }

        private void SaveManifestButton_Click(object sender, EventArgs e)
        {

            if (SaveFileDialog.ShowDialog() != DialogResult.OK) return;

            try
            {
                var filename = SaveFileDialog.FileName;
                var xml = ProjectManifestTextBox.Text;
                var xmldoc = new XmlDocument();
                xmldoc.LoadXml(xml);
                var pso = XmlPso.GetPso(xmldoc);
                var bytes = pso.Save();
                File.WriteAllBytes(filename, bytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving _manifest.ymf file:\n" + ex.ToString());
            }

        }

        private string LoadManifestXml(string filename)
        {
            EnsureProjectManifestHashNames();

            var data = File.ReadAllBytes(filename);
            if (IsXml(data))
            {
                var xml = File.ReadAllText(filename);
                return DecodeManifestXml(xml);
            }

            var entry = new RpfBinaryFileEntry();
            entry.Name = Path.GetFileName(filename);
            entry.NameLower = entry.Name.ToLowerInvariant();
            entry.Path = filename;
            entry.FileSize = (uint)data.Length;
            entry.FileUncompressedSize = (uint)data.Length;

            var ymf = new YmfFile();
            ymf.Load(data, entry);

            string xmlFilename;
            var xmlText = MetaXml.GetXml(ymf, out xmlFilename);
            if (string.IsNullOrEmpty(xmlText))
            {
                throw new Exception("The selected file could not be decoded as a supported manifest YMF.");
            }

            return xmlText;
        }

        private string DecodeManifestXml(string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            if (doc.DocumentElement?.Name != "CPackFileMetaData")
            {
                return xml;
            }

            var pso = XmlPso.GetPso(doc);
            return PsoXml.GetXml(pso);
        }

        private void EnsureProjectManifestHashNames()
        {
            var project = CurrentProjectFile ?? ProjectForm?.CurrentProjectFile;
            if (project == null) return;

            foreach (var filename in project.YmapFilenames) EnsureManifestHashName(filename);
            foreach (var filename in project.YtypFilenames) EnsureManifestHashName(filename);
            foreach (var filename in project.YbnFilenames) EnsureManifestHashName(filename);
            foreach (var filename in project.YtdFilenames) EnsureManifestHashName(filename);
            foreach (var filename in project.YdrFilenames) EnsureManifestHashName(filename);
            foreach (var filename in project.YddFilenames) EnsureManifestHashName(filename);
            foreach (var filename in project.YftFilenames) EnsureManifestHashName(filename);

            foreach (var ymap in project.YmapFiles)
            {
                EnsureManifestHashName(ymap?.Name);
                EnsureManifestHashName(ymap?.RpfFileEntry?.Name);
                EnsureManifestHashName(ymap?.RpfFileEntry?.NameLower);
                EnsureManifestHashName(ymap?.FilePath);

                if (ymap?.AllEntities != null)
                {
                    foreach (var ent in ymap.AllEntities)
                    {
                        EnsureManifestHashName(ent?.Archetype?.Name);
                        EnsureManifestHashName(ent?.Archetype?.AssetName);
                        EnsureManifestHashName(ent?.Archetype?.Ytyp?.RpfFileEntry?.Name);
                        EnsureManifestHashName(ent?.Archetype?.Ytyp?.RpfFileEntry?.NameLower);
                    }
                }
            }

            foreach (var ytyp in project.YtypFiles)
            {
                EnsureManifestHashName(ytyp?.Name);
                EnsureManifestHashName(ytyp?.RpfFileEntry?.Name);
                EnsureManifestHashName(ytyp?.RpfFileEntry?.NameLower);
                EnsureManifestHashName(ytyp?.FilePath);

                if (ytyp?.AllArchetypes == null) continue;

                foreach (var archetype in ytyp.AllArchetypes)
                {
                    EnsureManifestHashName(archetype?.Name);
                    EnsureManifestHashName(archetype?.AssetName);

                    var mlo = archetype as MloArchetype;
                    if (mlo != null)
                    {
                        EnsureManifestHashName(mlo.Name);
                    }
                }
            }

            foreach (var ybn in project.YbnFiles)
            {
                EnsureManifestHashName(ybn?.Name);
                EnsureManifestHashName(ybn?.RpfFileEntry?.Name);
                EnsureManifestHashName(ybn?.RpfFileEntry?.NameLower);
                EnsureManifestHashName(ybn?.FilePath);
            }
        }

        private static void EnsureManifestHashName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            JenkIndex.Ensure(name);

            var lowerName = name.ToLowerInvariant();
            if (lowerName != name)
            {
                JenkIndex.Ensure(lowerName);
            }

            var fileName = Path.GetFileName(name);
            if (!string.IsNullOrWhiteSpace(fileName) && (fileName != name))
            {
                JenkIndex.Ensure(fileName);

                var lowerFileName = fileName.ToLowerInvariant();
                if (lowerFileName != fileName)
                {
                    JenkIndex.Ensure(lowerFileName);
                }
            }

            var shortName = Path.GetFileNameWithoutExtension(fileName);
            if (!string.IsNullOrWhiteSpace(shortName))
            {
                JenkIndex.Ensure(shortName);

                var lowerShortName = shortName.ToLowerInvariant();
                if (lowerShortName != shortName)
                {
                    JenkIndex.Ensure(lowerShortName);
                }
            }
        }

        private static bool IsXml(byte[] data)
        {
            if (data == null) return false;

            for (int i = 0; i < data.Length; i++)
            {
                var c = (char)data[i];
                if (char.IsWhiteSpace(c)) continue;
                return c == '<';
            }

            return false;
        }

        private static void MergeManifestSection(XmlDocument currentDoc, XmlDocument generatedDoc, string sectionName, string keyName)
        {
            var currentRoot = currentDoc.DocumentElement;
            var generatedRoot = generatedDoc.DocumentElement;
            if ((currentRoot == null) || (generatedRoot == null)) return;

            var generatedSection = GetChildElement(generatedRoot, sectionName);
            if (generatedSection == null) return;

            var currentSection = GetChildElement(currentRoot, sectionName);
            if (currentSection == null)
            {
                InsertManifestSection(currentRoot, currentDoc.ImportNode(generatedSection, true), sectionName);
                return;
            }

            foreach (XmlNode node in generatedSection.ChildNodes)
            {
                var generatedItem = node as XmlElement;
                if ((generatedItem == null) || (generatedItem.Name != "Item")) continue;

                var importedItem = currentDoc.ImportNode(generatedItem, true);
                var currentItem = FindManifestItem(currentSection, keyName, GetManifestItemKey(generatedItem, keyName));

                if (currentItem != null)
                {
                    currentSection.ReplaceChild(importedItem, currentItem);
                }
                else
                {
                    currentSection.AppendChild(importedItem);
                }
            }
        }

        private static void InsertManifestSection(XmlElement currentRoot, XmlNode importedSection, string sectionName)
        {
            var previousSection = GetPreviousManifestSection(currentRoot, sectionName);
            if (previousSection?.NextSibling != null)
            {
                currentRoot.InsertBefore(importedSection, previousSection.NextSibling);
            }
            else
            {
                currentRoot.AppendChild(importedSection);
            }
        }

        private static XmlElement FindManifestItem(XmlElement section, string keyName, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            foreach (XmlNode node in section.ChildNodes)
            {
                var item = node as XmlElement;
                if ((item == null) || (item.Name != "Item")) continue;

                var itemKey = GetManifestItemKey(item, keyName);
                if (string.Equals(itemKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        private static string GetManifestItemKey(XmlElement item, string keyName)
        {
            return GetChildElement(item, keyName)?.InnerText;
        }

        private static XmlElement GetPreviousManifestSection(XmlElement root, string sectionName)
        {
            var sectionOrder = new[]
            {
                "MapDataGroups",
                "HDTxdBindingArray",
                "imapDependencies",
                "imapDependencies_2",
                "itypDependencies_2",
                "Interiors"
            };

            var sectionIndex = Array.IndexOf(sectionOrder, sectionName);
            for (int i = sectionIndex - 1; i >= 0; i--)
            {
                var section = GetChildElement(root, sectionOrder[i]);
                if (section != null) return section;
            }

            return null;
        }

        private static XmlElement GetChildElement(XmlElement parent, string name)
        {
            foreach (XmlNode node in parent.ChildNodes)
            {
                var element = node as XmlElement;
                if ((element != null) && (element.Name == name))
                {
                    return element;
                }
            }

            return null;
        }

        private static string FormatXml(XmlDocument doc)
        {
            var settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "  ";
            settings.NewLineChars = Environment.NewLine;
            settings.OmitXmlDeclaration = false;

            using (var writer = new StringWriter())
            using (var xmlWriter = XmlWriter.Create(writer, settings))
            {
                doc.Save(xmlWriter);
                return writer.ToString();
            }
        }
    }
}
