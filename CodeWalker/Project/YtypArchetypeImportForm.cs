using CodeWalker.GameFiles;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CodeWalker.Project
{
    public class YtypArchetypeImportForm : Form
    {
        private readonly YtypFile targetYtyp;
        private readonly List<YtypSourceItem> sourceItems = new List<YtypSourceItem>();
        private readonly ComboBox sourceComboBox = new ComboBox();
        private readonly Button browseButton = new Button();
        private readonly CheckedListBox archetypesCheckedListBox = new CheckedListBox();
        private readonly Button checkAllButton = new Button();
        private readonly Button checkCopiesButton = new Button();
        private readonly Button checkImportsButton = new Button();
        private readonly Button okButton = new Button();
        private readonly Button cancelButton = new Button();
        private readonly Label summaryLabel = new Label();

        public YtypFile SourceYtyp { get; private set; }
        public List<YtypArchetypeImportItem> SelectedItems { get; } = new List<YtypArchetypeImportItem>();

        public YtypArchetypeImportForm(YtypFile targetYtyp, IEnumerable<YtypFile> projectYtyps)
        {
            this.targetYtyp = targetYtyp;

            Text = "Copy/Import Archetypes From YTYP";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(760, 560);
            MinimizeBox = false;
            MaximizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;

            InitializeControls();

            foreach (var ytyp in projectYtyps ?? Enumerable.Empty<YtypFile>())
            {
                if ((ytyp == null) || (ytyp == targetYtyp)) continue;
                sourceItems.Add(new YtypSourceItem(ytyp, GetYtypDisplayName(ytyp)));
            }

            sourceComboBox.Items.AddRange(sourceItems.Cast<object>().ToArray());
            if (sourceComboBox.Items.Count > 0)
            {
                sourceComboBox.SelectedIndex = 0;
            }
        }

        private void InitializeControls()
        {
            var sourceLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 15),
                Text = "Source YTYP:"
            };

            sourceComboBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            sourceComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            sourceComboBox.Location = new Point(90, 12);
            sourceComboBox.Size = new Size(535, 21);
            sourceComboBox.SelectedIndexChanged += SourceComboBox_SelectedIndexChanged;

            browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browseButton.Location = new Point(635, 10);
            browseButton.Size = new Size(95, 24);
            browseButton.Text = "Browse...";
            browseButton.Click += BrowseButton_Click;

            summaryLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            summaryLabel.Location = new Point(12, 42);
            summaryLabel.Size = new Size(718, 18);

            archetypesCheckedListBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            archetypesCheckedListBox.CheckOnClick = true;
            archetypesCheckedListBox.HorizontalScrollbar = true;
            archetypesCheckedListBox.Location = new Point(12, 66);
            archetypesCheckedListBox.Size = new Size(718, 402);

            checkAllButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            checkAllButton.Location = new Point(12, 480);
            checkAllButton.Size = new Size(82, 24);
            checkAllButton.Text = "Check All";
            checkAllButton.Click += (s, e) => SetCheckedItems(item => true);

            checkCopiesButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            checkCopiesButton.Location = new Point(100, 480);
            checkCopiesButton.Size = new Size(92, 24);
            checkCopiesButton.Text = "Copy Only";
            checkCopiesButton.Click += (s, e) => SetCheckedItems(item => item.TargetArchetype != null);

            checkImportsButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            checkImportsButton.Location = new Point(198, 480);
            checkImportsButton.Size = new Size(92, 24);
            checkImportsButton.Text = "Import Only";
            checkImportsButton.Click += (s, e) => SetCheckedItems(item => item.TargetArchetype == null);

            okButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            okButton.Location = new Point(570, 480);
            okButton.Size = new Size(75, 24);
            okButton.Text = "OK";
            okButton.Click += OkButton_Click;

            cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(655, 480);
            cancelButton.Size = new Size(75, 24);
            cancelButton.Text = "Cancel";

            AcceptButton = okButton;
            CancelButton = cancelButton;

            Controls.Add(sourceLabel);
            Controls.Add(sourceComboBox);
            Controls.Add(browseButton);
            Controls.Add(summaryLabel);
            Controls.Add(archetypesCheckedListBox);
            Controls.Add(checkAllButton);
            Controls.Add(checkCopiesButton);
            Controls.Add(checkImportsButton);
            Controls.Add(okButton);
            Controls.Add(cancelButton);
        }

        private void SourceComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = sourceComboBox.SelectedItem as YtypSourceItem;
            SourceYtyp = item?.Ytyp;
            PopulateArchetypes();
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Ytyp files|*.ytyp";
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                var ytyp = new YtypFile();
                try
                {
                    ytyp.Load(File.ReadAllBytes(dialog.FileName));
                    ytyp.Name = Path.GetFileName(dialog.FileName);
                    ytyp.FilePath = dialog.FileName;
                    ytyp.RpfFileEntry = new RpfResourceFileEntry
                    {
                        Name = ytyp.Name,
                        NameLower = ytyp.Name.ToLowerInvariant(),
                        Path = dialog.FileName
                    };
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Error loading source ytyp:\n" + ex.Message);
                    return;
                }

                var sourceItem = new YtypSourceItem(ytyp, Path.GetFileName(dialog.FileName));
                sourceItems.Add(sourceItem);
                sourceComboBox.Items.Add(sourceItem);
                sourceComboBox.SelectedItem = sourceItem;
            }
        }

        private void PopulateArchetypes()
        {
            archetypesCheckedListBox.Items.Clear();

            var sourceArchetypes = SourceYtyp?.AllArchetypes ?? new Archetype[0];
            var targetArchetypes = targetYtyp?.AllArchetypes ?? new Archetype[0];
            var targetByAssetName = BuildArchetypeLookup(targetArchetypes, true);
            var targetByName = BuildArchetypeLookup(targetArchetypes, false);
            int copyCount = 0;
            int importCount = 0;

            foreach (var sourceArchetype in sourceArchetypes)
            {
                var targetArchetype = FindMatchingArchetype(sourceArchetype, targetByAssetName, targetByName);
                var item = new YtypArchetypeImportItem(sourceArchetype, targetArchetype);
                archetypesCheckedListBox.Items.Add(item, true);

                if (targetArchetype != null) copyCount++;
                else importCount++;
            }

            summaryLabel.Text = sourceArchetypes.Length.ToString() + " archetypes: " +
                                copyCount.ToString() + " will copy into existing, " +
                                importCount.ToString() + " will import as new.";
        }

        private void SetCheckedItems(Func<YtypArchetypeImportItem, bool> shouldCheck)
        {
            for (int i = 0; i < archetypesCheckedListBox.Items.Count; i++)
            {
                var item = archetypesCheckedListBox.Items[i] as YtypArchetypeImportItem;
                archetypesCheckedListBox.SetItemChecked(i, (item != null) && shouldCheck(item));
            }
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            SelectedItems.Clear();

            foreach (var item in archetypesCheckedListBox.CheckedItems)
            {
                var importItem = item as YtypArchetypeImportItem;
                if (importItem != null)
                {
                    SelectedItems.Add(importItem);
                }
            }

            if (SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Select at least one archetype to copy or import.");
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private static Dictionary<MetaHash, Archetype> BuildArchetypeLookup(IEnumerable<Archetype> archetypes, bool useAssetName)
        {
            var lookup = new Dictionary<MetaHash, Archetype>();
            foreach (var archetype in archetypes ?? Enumerable.Empty<Archetype>())
            {
                var key = useAssetName ? archetype._BaseArchetypeDef.assetName : archetype._BaseArchetypeDef.name;
                if (key == 0) continue;
                if (!lookup.ContainsKey(key))
                {
                    lookup.Add(key, archetype);
                }
            }
            return lookup;
        }

        private static Archetype FindMatchingArchetype(Archetype sourceArchetype, Dictionary<MetaHash, Archetype> targetByAssetName, Dictionary<MetaHash, Archetype> targetByName)
        {
            if (sourceArchetype == null) return null;

            Archetype targetArchetype;
            var assetName = sourceArchetype._BaseArchetypeDef.assetName;
            if ((assetName != 0) && targetByAssetName.TryGetValue(assetName, out targetArchetype))
            {
                return targetArchetype;
            }

            var name = sourceArchetype._BaseArchetypeDef.name;
            if ((name != 0) && targetByName.TryGetValue(name, out targetArchetype))
            {
                return targetArchetype;
            }

            return null;
        }

        private static string GetYtypDisplayName(YtypFile ytyp)
        {
            return ytyp?.RpfFileEntry?.Name ?? ytyp?.Name ?? ytyp?.FilePath ?? "(unnamed ytyp)";
        }

        private class YtypSourceItem
        {
            public YtypFile Ytyp { get; }
            private readonly string displayName;

            public YtypSourceItem(YtypFile ytyp, string displayName)
            {
                Ytyp = ytyp;
                this.displayName = displayName;
            }

            public override string ToString()
            {
                return displayName;
            }
        }
    }

    public class YtypArchetypeImportItem
    {
        public Archetype SourceArchetype { get; }
        public Archetype TargetArchetype { get; }

        public YtypArchetypeImportItem(Archetype sourceArchetype, Archetype targetArchetype)
        {
            SourceArchetype = sourceArchetype;
            TargetArchetype = targetArchetype;
        }

        public override string ToString()
        {
            var action = TargetArchetype != null ? "Copy" : "Import";
            var name = SourceArchetype?.Name ?? string.Empty;
            var assetName = SourceArchetype?.AssetName ?? string.Empty;
            var extensions = SourceArchetype?.Extensions?.Length ?? 0;
            return action + " | " + name + " | asset: " + assetName + " | extensions: " + extensions.ToString();
        }
    }
}
