using CodeWalker.Updates;
using System.Drawing;
using System.Windows.Forms;

namespace CodeWalker.Project
{
    public class UpdateAvailableForm : Form
    {
        private readonly TextBox detailsTextBox = new TextBox();
        private readonly Button updateButton = new Button();
        private readonly Button cancelButton = new Button();

        public UpdateAvailableForm(UpdateCheckResult update)
        {
            Text = "BadWalker Update Available";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(720, 520);
            MinimumSize = new Size(560, 360);
            MinimizeBox = false;
            MaximizeBox = true;
            ShowIcon = false;
            ShowInTaskbar = false;

            var summaryLabel = new Label
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(12, 12),
                Size = new Size(680, 36),
                Text = update?.Summary ?? "A BadWalker update is available."
            };

            detailsTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            detailsTextBox.Location = new Point(12, 54);
            detailsTextBox.Multiline = true;
            detailsTextBox.ReadOnly = true;
            detailsTextBox.ScrollBars = ScrollBars.Both;
            detailsTextBox.WordWrap = false;
            detailsTextBox.Size = new Size(680, 370);
            detailsTextBox.Text = GitHubUpdateService.BuildDetailsText(update);

            updateButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            updateButton.Location = new Point(498, 436);
            updateButton.Size = new Size(94, 26);
            updateButton.Text = "Update";
            updateButton.DialogResult = DialogResult.OK;

            cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancelButton.Location = new Point(598, 436);
            cancelButton.Size = new Size(94, 26);
            cancelButton.Text = "Cancel";
            cancelButton.DialogResult = DialogResult.Cancel;

            Controls.Add(summaryLabel);
            Controls.Add(detailsTextBox);
            Controls.Add(updateButton);
            Controls.Add(cancelButton);

            AcceptButton = updateButton;
            CancelButton = cancelButton;
        }
    }
}
