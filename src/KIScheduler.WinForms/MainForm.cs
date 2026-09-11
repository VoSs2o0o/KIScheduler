namespace KIScheduler.WinForms;

public sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "KIScheduler";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(640, 360);
        ClientSize = new Size(960, 540);

        Controls.Add(new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 16, FontStyle.Regular),
            Location = new Point(24, 24),
            Text = "KIScheduler ist bereit."
        });
    }
}
