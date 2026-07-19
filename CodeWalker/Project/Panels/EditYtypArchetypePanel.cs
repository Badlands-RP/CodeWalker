using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using CodeWalker.GameFiles;
using SharpDX;

namespace CodeWalker.Project.Panels
{
    public partial class EditYtypArchetypePanel : ProjectPanel
    {
        public ProjectForm ProjectForm;

        private bool populatingui;
        private TabPage ExtensionsTabPage;
        private ListBox ExtensionsListBox;
        private TextBox ExtensionDetailsTextBox;

        public EditYtypArchetypePanel(ProjectForm owner)
        {
            InitializeComponent();
            InitializeExtensionsTab();

            ProjectForm = owner;
        }

        public Archetype CurrentArchetype { get; set; }

        private void EditYtypArchetypePanel_Load(object sender, EventArgs e)
        {
            AssetTypeComboBox.Items.AddRange(Enum.GetNames(typeof(rage__fwArchetypeDef__eAssetType)));
        }

        public void SetArchetype(Archetype archetype)
        {
            CurrentArchetype = archetype;
            Tag = archetype;
            UpdateFormTitle();
            UpdateControls();
        }

        private void UpdateFormTitle()
        {
            Text = CurrentArchetype?.Name ?? "Edit Archetype";
        }

        private void UpdateControls()
        {
            if (CurrentArchetype != null)
            {
                ArchetypeDeleteButton.Enabled = ProjectForm.YtypExistsInProject(CurrentArchetype.Ytyp);
                ArchetypeNameTextBox.Text = CurrentArchetype.Name;
                AssetNameTextBox.Text = CurrentArchetype.AssetName;
                LodDistNumericUpDown.Value = (decimal)CurrentArchetype._BaseArchetypeDef.lodDist;
                HDTextureDistNumericUpDown.Value = (decimal)CurrentArchetype._BaseArchetypeDef.hdTextureDist;
                SpecialAttributeNumericUpDown.Value = CurrentArchetype._BaseArchetypeDef.specialAttribute;
                ArchetypeFlagsTextBox.Text = CurrentArchetype._BaseArchetypeDef.flags.ToString();
                TextureDictTextBox.Text = CurrentArchetype._BaseArchetypeDef.textureDictionary.ToCleanString();
                ClipDictionaryTextBox.Text = CurrentArchetype._BaseArchetypeDef.clipDictionary.ToCleanString();
                DrawableDictionaryTextBox.Text = CurrentArchetype._BaseArchetypeDef.drawableDictionary.ToCleanString();
                PhysicsDictionaryTextBox.Text = CurrentArchetype._BaseArchetypeDef.physicsDictionary.ToCleanString();
                AssetTypeComboBox.Text = CurrentArchetype._BaseArchetypeDef.assetType.ToString();
                BBMinTextBox.Text = FloatUtil.GetVector3String(CurrentArchetype._BaseArchetypeDef.bbMin);
                BBMaxTextBox.Text = FloatUtil.GetVector3String(CurrentArchetype._BaseArchetypeDef.bbMax);
                BSCenterTextBox.Text = FloatUtil.GetVector3String(CurrentArchetype._BaseArchetypeDef.bsCentre);
                BSRadiusTextBox.Text = FloatUtil.ToString(CurrentArchetype._BaseArchetypeDef.bsRadius);

                if (CurrentArchetype is MloArchetype MloArchetype)
                {
                    if (!TabControl.TabPages.Contains(MloArchetypeTabPage))
                    {
                        TabControl.TabPages.Add(MloArchetypeTabPage);
                    }

                    //MloInstanceData mloinstance = ProjectForm.TryGetMloInstance(MloArchetype);
                    //nothing to see here right now
                }
                else TabControl.TabPages.Remove(MloArchetypeTabPage);



                if (CurrentArchetype is TimeArchetype TimeArchetype)
                {
                    if (!TabControl.TabPages.Contains(TimeArchetypeTabPage))
                    {
                        TabControl.TabPages.Add(TimeArchetypeTabPage);
                    }

                    TimeFlagsTextBox.Text = TimeArchetype.TimeFlags.ToString();

                }
                else TabControl.TabPages.Remove(TimeArchetypeTabPage);

            }

            UpdateExtensionsTab();
        }

        private void InitializeExtensionsTab()
        {
            ExtensionsTabPage = new TabPage();
            ExtensionsListBox = new ListBox();
            ExtensionDetailsTextBox = new TextBox();

            ExtensionsTabPage.Text = "Extensions";
            ExtensionsTabPage.UseVisualStyleBackColor = true;

            ExtensionsListBox.Dock = DockStyle.Left;
            ExtensionsListBox.Width = 260;
            ExtensionsListBox.DisplayMember = "DisplayText";
            ExtensionsListBox.SelectedIndexChanged += ExtensionsListBox_SelectedIndexChanged;

            ExtensionDetailsTextBox.Dock = DockStyle.Fill;
            ExtensionDetailsTextBox.Multiline = true;
            ExtensionDetailsTextBox.ReadOnly = true;
            ExtensionDetailsTextBox.ScrollBars = ScrollBars.Both;
            ExtensionDetailsTextBox.WordWrap = false;
            ExtensionDetailsTextBox.Font = new System.Drawing.Font("Courier New", 9.0F);

            ExtensionsTabPage.Controls.Add(ExtensionDetailsTextBox);
            ExtensionsTabPage.Controls.Add(ExtensionsListBox);
            TabControl.TabPages.Add(ExtensionsTabPage);
        }

        private void UpdateExtensionsTab()
        {
            ExtensionsListBox.Items.Clear();
            ExtensionDetailsTextBox.Clear();

            var extensions = CurrentArchetype?.Extensions;
            if ((extensions == null) || (extensions.Length == 0))
            {
                ExtensionDetailsTextBox.Text = "No extensions.";
                return;
            }

            for (int i = 0; i < extensions.Length; i++)
            {
                ExtensionsListBox.Items.Add(new ExtensionListItem(i, extensions[i]));
            }

            if (ExtensionsListBox.Items.Count > 0)
            {
                ExtensionsListBox.SelectedIndex = 0;
            }
        }

        private void ExtensionsListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = ExtensionsListBox.SelectedItem as ExtensionListItem;
            ExtensionDetailsTextBox.Text = GetExtensionDetails(item?.Extension);
        }

        private string GetExtensionDetails(MetaWrapper extension)
        {
            if (extension == null) return string.Empty;

            var sb = new StringBuilder();
            AppendLine(sb, "Type", extension.GetType().Name);
            AppendLine(sb, "Name", extension.Name);
            sb.AppendLine();

            if (extension is MCExtensionDefParticleEffect particle)
            {
                AppendLine(sb, "Data.name", particle.Data.name.ToCleanString());
                AppendLine(sb, "fxName", particle.fxName);
                AppendLine(sb, "Data.offsetPosition", FormatVector3(particle.Data.offsetPosition));
                AppendLine(sb, "Data.offsetRotation", FormatVector4(particle.Data.offsetRotation));
                AppendLine(sb, "Data.fxType", particle.Data.fxType.ToString());
                AppendLine(sb, "Data.boneTag", particle.Data.boneTag.ToString());
                AppendLine(sb, "Data.scale", FloatUtil.ToString(particle.Data.scale));
                AppendLine(sb, "Data.probability", particle.Data.probability.ToString());
                AppendLine(sb, "Data.flags", particle.Data.flags.ToString());
                AppendLine(sb, "Data.color", "0x" + particle.Data.color.ToString("X8"));
            }
            else if (extension is MCExtensionDefLightEffect light)
            {
                AppendLine(sb, "Data.name", light.Data.name.ToCleanString());
                AppendLine(sb, "Data.offsetPosition", FormatVector3(light.Data.offsetPosition));
                AppendLine(sb, "Light instances", (light.instances?.Length ?? 0).ToString());
            }
            else
            {
                AppendGenericObjectDump(sb, extension);
            }

            return sb.ToString();
        }

        private static void AppendGenericObjectDump(StringBuilder sb, object value)
        {
            if (value == null) return;

            var type = value.GetType();
            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (prop.GetIndexParameters().Length > 0) continue;

                object propValue;
                try
                {
                    propValue = prop.GetValue(value, null);
                }
                catch
                {
                    propValue = "(unavailable)";
                }

                AppendLine(sb, prop.Name, FormatValue(propValue));
            }

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                object fieldValue;
                try
                {
                    fieldValue = field.GetValue(value);
                }
                catch
                {
                    fieldValue = "(unavailable)";
                }

                AppendLine(sb, field.Name, FormatValue(fieldValue));
            }
        }

        private static string FormatValue(object value)
        {
            if (value == null) return string.Empty;
            if (value is Vector3 v3) return FormatVector3(v3);
            if (value is Vector4 v4) return FormatVector4(v4);
            if (value is MetaHash hash) return hash.ToCleanString();
            if (value is Array arr) return value.GetType().GetElementType()?.Name + "[" + arr.Length.ToString() + "]";
            return value.ToString();
        }

        private static string FormatVector3(Vector3 v)
        {
            return FloatUtil.GetVector3String(v);
        }

        private static string FormatVector4(Vector4 v)
        {
            return FloatUtil.GetVector4String(v);
        }

        private static void AppendLine(StringBuilder sb, string name, string value)
        {
            sb.Append(name.PadRight(24));
            sb.Append(": ");
            sb.AppendLine(value ?? string.Empty);
        }

        private class ExtensionListItem
        {
            public MetaWrapper Extension { get; }
            public string DisplayText { get; }

            public ExtensionListItem(int index, MetaWrapper extension)
            {
                Extension = extension;
                DisplayText = (index + 1).ToString() + ". " + extension.GetType().Name + " - " + extension.Name;
            }
        }

        private void ArchetypeFlagsTextBox_TextChanged(object sender, EventArgs e)
        {
            if (populatingui) return;
            if (CurrentArchetype == null) return;
            uint flags = 0;
            uint.TryParse(ArchetypeFlagsTextBox.Text, out flags);
            populatingui = true;
            for (int i = 0; i < EntityFlagsCheckedListBox.Items.Count; i++)
            {
                var c = ((flags & (1u << i)) > 0);
                EntityFlagsCheckedListBox.SetItemCheckState(i, c ? CheckState.Checked : CheckState.Unchecked);
            }
            populatingui = false;
            lock (ProjectForm.ProjectSyncRoot)
            {
                if (CurrentArchetype._BaseArchetypeDef.flags != flags)
                {
                    CurrentArchetype._BaseArchetypeDef.flags = flags;
                    ProjectForm.SetYtypHasChanged(true);
                }
            }
        }

        private void ArchetypeFlagsCheckedListBox_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (populatingui) return;
            if (CurrentArchetype == null) return;
            uint flags = 0;
            for (int i = 0; i < EntityFlagsCheckedListBox.Items.Count; i++)
            {
                if (e.Index == i)
                {
                    if (e.NewValue == CheckState.Checked)
                    {
                        flags += (uint)(1 << i);
                    }
                }
                else
                {
                    if (EntityFlagsCheckedListBox.GetItemChecked(i))
                    {
                        flags += (uint)(1 << i);
                    }
                }
            }
            populatingui = true;
            ArchetypeFlagsTextBox.Text = flags.ToString();
            populatingui = false;
            lock (ProjectForm.ProjectSyncRoot)
            {
                if (CurrentArchetype._BaseArchetypeDef.flags != flags)
                {
                    CurrentArchetype._BaseArchetypeDef.flags = flags;
                    ProjectForm.SetYtypHasChanged(true);
                }
            }
        }

        private void ArchetypeNameTextBox_TextChanged(object sender, EventArgs e)
        {
            if (ProjectForm == null) return;

            var hash = 0u;
            if (!uint.TryParse(ArchetypeNameTextBox.Text, out hash))//don't re-hash hashes
            {
                hash = JenkHash.GenHash(ArchetypeNameTextBox.Text);
            }

            if (CurrentArchetype._BaseArchetypeDef.name != hash)
            {
                CurrentArchetype._BaseArchetypeDef.name = hash;
                UpdateFormTitle();

                TreeNode tn = ProjectForm.ProjectExplorer?.FindArchetypeTreeNode(CurrentArchetype);
                if (tn != null)
                    tn.Text = ArchetypeNameTextBox.Text ?? "0"; // using the text box text because the name may not be in the gfc.

                ProjectForm.SetYtypHasChanged(true);
            }
        }

        private void AssetNameTextBox_TextChanged(object sender, EventArgs e)
        {
            if (ProjectForm == null) return;

            var hash = 0u;
            if (!uint.TryParse(AssetNameTextBox.Text, out hash))//don't re-hash hashes
            {
                hash = JenkHash.GenHash(AssetNameTextBox.Text);
            }

            if (CurrentArchetype._BaseArchetypeDef.assetName != hash)
            {
                CurrentArchetype._BaseArchetypeDef.assetName = hash;
                ProjectForm.SetYtypHasChanged(true);
            }
        }

        private void TextureDictTextBox_TextChanged(object sender, EventArgs e)
        {
            if (ProjectForm == null) return;

            lock (ProjectForm.ProjectSyncRoot)
            {
                // Embedded...
                if (TextureDictTextBox.Text == ArchetypeNameTextBox.Text)
                {
                    TextureDictHashLabel.Text = "Embedded";
                    CurrentArchetype._BaseArchetypeDef.textureDictionary = CurrentArchetype._BaseArchetypeDef.name;
                    return;
                }

                var hash = 0u;
                if (!uint.TryParse(TextureDictTextBox.Text, out hash))//don't re-hash hashes
                {
                    hash = JenkHash.GenHash(TextureDictTextBox.Text);
                }

                if (CurrentArchetype._BaseArchetypeDef.textureDictionary != hash)
                {
                    CurrentArchetype._BaseArchetypeDef.textureDictionary = hash;
                    var ytd = ProjectForm.GameFileCache.GetYtd(hash);
                    if (ytd == null)
                    {
                        TextureDictHashLabel.Text = "# " + hash.ToString() + " (invalid)";
                        ProjectForm.SetYtypHasChanged(true);
                        return;
                    }
                    ProjectForm.SetYtypHasChanged(true);
                }
                TextureDictHashLabel.Text = "# " + hash.ToString();
            }
        }

        private void ClipDictionaryTextBox_TextChanged(object sender, EventArgs e)
        {
            if (ProjectForm == null) return;

            var hash = 0u;
            if (!uint.TryParse(ClipDictionaryTextBox.Text, out hash))//don't re-hash hashes
            {
                hash = JenkHash.GenHash(ClipDictionaryTextBox.Text);
            }

            if (CurrentArchetype._BaseArchetypeDef.clipDictionary != hash)
            {
                CurrentArchetype._BaseArchetypeDef.clipDictionary = hash;
                ProjectForm.SetYtypHasChanged(true);
            }
        }

        private void DrawableDictionaryTextBox_TextChanged(object sender, EventArgs e)
        {
            if (ProjectForm == null) return;

            lock (ProjectForm.ProjectSyncRoot)
            {
                var hash = 0u;
                if (!uint.TryParse(DrawableDictionaryTextBox.Text, out hash))//don't re-hash hashes
                {
                    hash = JenkHash.GenHash(DrawableDictionaryTextBox.Text);
                }

                if (CurrentArchetype._BaseArchetypeDef.drawableDictionary != hash)
                {
                    CurrentArchetype._BaseArchetypeDef.drawableDictionary = hash;
                    var ydd = ProjectForm.GameFileCache.GetYdd(hash);
                    if (ydd == null)
                    {
                        DrawableDictHashLabel.Text = "# " + hash.ToString() + " (invalid)";
                        ProjectForm.SetYtypHasChanged(true);
                        return;
                    }
                    ProjectForm.SetYtypHasChanged(true);
                }
                DrawableDictHashLabel.Text = "# " + hash.ToString();
            }
        }

        private void PhysicsDictionaryTextBox_TextChanged(object sender, EventArgs e)
        {
            if (ProjectForm == null) return;

            lock (ProjectForm.ProjectSyncRoot)
            {
                // Embedded...
                if (PhysicsDictionaryTextBox.Text == ArchetypeNameTextBox.Text)
                {
                    PhysicsDictHashLabel.Text = "Embedded";
                    CurrentArchetype._BaseArchetypeDef.physicsDictionary = CurrentArchetype._BaseArchetypeDef.name;
                    return;
                }

                var hash = 0u;
                if (!uint.TryParse(PhysicsDictionaryTextBox.Text, out hash))//don't re-hash hashes
                {
                    hash = JenkHash.GenHash(PhysicsDictionaryTextBox.Text);
                }

                if (CurrentArchetype._BaseArchetypeDef.physicsDictionary != hash)
                {
                    CurrentArchetype._BaseArchetypeDef.physicsDictionary = hash;
                    var ybn = ProjectForm.GameFileCache.GetYbn(hash);
                    if (ybn == null)
                    {
                        PhysicsDictHashLabel.Text = "# " + hash.ToString() + " (invalid)";
                        ProjectForm.SetYtypHasChanged(true);
                        return;
                    }
                    ProjectForm.SetYtypHasChanged(true);
                }
                PhysicsDictHashLabel.Text = "# " + hash.ToString();
            }
        }

        private void LodDistNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            var loddist = (float)LodDistNumericUpDown.Value;
            if (!MathUtil.NearEqual(loddist, CurrentArchetype._BaseArchetypeDef.lodDist))
            {
                CurrentArchetype._BaseArchetypeDef.lodDist = loddist;
                ProjectForm.SetYtypHasChanged(true);
            }
        }

        private void HDTextureDistNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            var hddist = (float)HDTextureDistNumericUpDown.Value;
            if (!MathUtil.NearEqual(hddist, CurrentArchetype._BaseArchetypeDef.hdTextureDist))
            {
                CurrentArchetype._BaseArchetypeDef.hdTextureDist = hddist;
                ProjectForm.SetYtypHasChanged(true);
            }
        }

        private void SpecialAttributeNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            var att = (uint)SpecialAttributeNumericUpDown.Value;
            if (CurrentArchetype._BaseArchetypeDef.specialAttribute != att)
            {
                CurrentArchetype._BaseArchetypeDef.specialAttribute = att;
                ProjectForm.SetYtypHasChanged(true);
            }
        }

        private void BBMinTextBox_TextChanged(object sender, EventArgs e)
        {
            Vector3 min = FloatUtil.ParseVector3String(BBMinTextBox.Text);
            if (CurrentArchetype._BaseArchetypeDef.bbMin != min)
            {
                CurrentArchetype._BaseArchetypeDef.bbMin = min;
                ProjectForm.SetYtypHasChanged(true);
            }
        }

        private void BBMaxTextBox_TextChanged(object sender, EventArgs e)
        {
            Vector3 max = FloatUtil.ParseVector3String(BBMaxTextBox.Text);

            if (CurrentArchetype._BaseArchetypeDef.bbMax != max)
            {
                CurrentArchetype._BaseArchetypeDef.bbMax = max;
                ProjectForm.SetYtypHasChanged(true);
            }
        }

        private void BSCenterTextBox_TextChanged(object sender, EventArgs e)
        {
            Vector3 c = FloatUtil.ParseVector3String(BSCenterTextBox.Text);

            if (CurrentArchetype._BaseArchetypeDef.bsCentre != c)
            {
                CurrentArchetype._BaseArchetypeDef.bsCentre = c;
                ProjectForm.SetYtypHasChanged(true);
            }
        }

        private void BSRadiusTextBox_TextChanged(object sender, EventArgs e)
        {
            if (FloatUtil.TryParse(BSRadiusTextBox.Text, out float f))
            {
                if (!MathUtil.NearEqual(CurrentArchetype._BaseArchetypeDef.bsRadius, f))
                {
                    CurrentArchetype._BaseArchetypeDef.bsRadius = f;
                    ProjectForm.SetYtypHasChanged(true);
                }
            }
            else
            {
                CurrentArchetype._BaseArchetypeDef.bsRadius = 0f;
                ProjectForm.SetYtypHasChanged(true);
            }
        }

        private void DeleteArchetypeButton_Click(object sender, EventArgs e)
        {
            ProjectForm.SetProjectItem(CurrentArchetype);
            ProjectForm.DeleteArchetype();
        }

        private void MloUpdatePortalCountsButton_Click(object sender, EventArgs e)
        {
            var mlo = CurrentArchetype as MloArchetype;
            if (mlo == null) return;

            mlo.UpdatePortalCounts();
        }

        private void TimeFlagsTextBox_TextChanged(object sender, EventArgs e)
        {
            if (populatingui) return;
            if (CurrentArchetype == null) return;
            if (CurrentArchetype is TimeArchetype TimeArchetype)
            {
                uint flags = 0;
                uint.TryParse(TimeFlagsTextBox.Text, out flags);
                populatingui = true;
                for (int i = 0; i < TimeFlagsCheckedListBox.Items.Count; i++)
                {
                    var c = ((flags & (1u << i)) > 0);
                    TimeFlagsCheckedListBox.SetItemCheckState(i, c ? CheckState.Checked : CheckState.Unchecked);
                }
                populatingui = false;
                lock (ProjectForm.ProjectSyncRoot)
                {
                    if (TimeArchetype.TimeFlags != flags)
                    {
                        TimeArchetype.SetTimeFlags(flags);
                        ProjectForm.SetYtypHasChanged(true);
                    }
                }
            }

        }

        private void TimeFlagsCheckedListBox_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (populatingui) return;
            if (CurrentArchetype == null) return;
            if (CurrentArchetype is TimeArchetype TimeArchetype)
            {
                uint flags = 0;
                for (int i = 0; i < TimeFlagsCheckedListBox.Items.Count; i++)
                {
                    if (e.Index == i)
                    {
                        if (e.NewValue == CheckState.Checked)
                        {
                            flags += (uint)(1 << i);
                        }
                    }
                    else
                    {
                        if (TimeFlagsCheckedListBox.GetItemChecked(i))
                        {
                            flags += (uint)(1 << i);
                        }
                    }
                }
                populatingui = true;
                TimeFlagsTextBox.Text = flags.ToString();
                populatingui = false;
                lock (ProjectForm.ProjectSyncRoot)
                {
                    if (TimeArchetype.TimeFlags != flags)
                    {
                        TimeArchetype.SetTimeFlags(flags);
                        ProjectForm.SetYtypHasChanged(true);
                    }
                }
            }
        }
    }
}
