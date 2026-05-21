namespace ArmWrestlingGame
{
    partial class SinglePlayerForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button btnTheme;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Button btnExit;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.btnStop = new System.Windows.Forms.Button();
            this.btnReset = new System.Windows.Forms.Button();
            this.btnTheme = new System.Windows.Forms.Button();
            this.btnExit = new System.Windows.Forms.Button();
            this.SuspendLayout();

            int btnWidth = 180;
            int btnHeight = 50;
            int startY = 30;
            int gap = 70;

            // btnStop
            this.btnStop.Location = new System.Drawing.Point(30, startY);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(btnWidth, btnHeight);
            this.btnStop.TabIndex = 0;
            this.btnStop.TabStop = false;
            this.btnStop.Text = "Stop";
            this.btnStop.Font = new System.Drawing.Font("Consolas", 14F, System.Drawing.FontStyle.Bold);
            this.btnStop.UseVisualStyleBackColor = false;
            this.btnStop.BackColor = System.Drawing.Color.FromArgb(50, 50, 50);
            this.btnStop.ForeColor = System.Drawing.Color.White;
            this.btnStop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnStop.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);

            // btnReset
            this.btnReset.Location = new System.Drawing.Point(30, startY + gap);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(btnWidth, btnHeight);
            this.btnReset.TabIndex = 1;
            this.btnReset.TabStop = false;
            this.btnReset.Text = "Reset";
            this.btnReset.Font = new System.Drawing.Font("Consolas", 14F, System.Drawing.FontStyle.Bold);
            this.btnReset.UseVisualStyleBackColor = false;
            this.btnReset.BackColor = System.Drawing.Color.FromArgb(50, 50, 50);
            this.btnReset.ForeColor = System.Drawing.Color.White;
            this.btnReset.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnReset.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);

            // btnTheme
            this.btnTheme.Location = new System.Drawing.Point(30, startY + gap * 2);
            this.btnTheme.Name = "btnTheme";
            this.btnTheme.Size = new System.Drawing.Size(btnWidth, btnHeight);
            this.btnTheme.TabIndex = 2;
            this.btnTheme.TabStop = false;
            this.btnTheme.Text = "Theme";
            this.btnTheme.Font = new System.Drawing.Font("Consolas", 14F, System.Drawing.FontStyle.Bold);
            this.btnTheme.UseVisualStyleBackColor = false;
            this.btnTheme.BackColor = System.Drawing.Color.FromArgb(50, 50, 50);
            this.btnTheme.ForeColor = System.Drawing.Color.White;
            this.btnTheme.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnTheme.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnTheme.Click += new System.EventHandler(this.btnTheme_Click);

            // btnExit
            this.btnExit.Location = new System.Drawing.Point(30, startY + gap * 3);
            this.btnExit.Name = "btnExit";
            this.btnExit.Size = new System.Drawing.Size(btnWidth, btnHeight);
            this.btnExit.TabIndex = 3;
            this.btnExit.TabStop = false;
            this.btnExit.Text = "Exit";
            this.btnExit.Font = new System.Drawing.Font("Consolas", 14F, System.Drawing.FontStyle.Bold);
            this.btnExit.UseVisualStyleBackColor = false;
            this.btnExit.BackColor = System.Drawing.Color.FromArgb(50, 50, 50);
            this.btnExit.ForeColor = System.Drawing.Color.White;
            this.btnExit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExit.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnExit.Click += new System.EventHandler(this.btnExit_Click);

            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Controls.Add(this.btnExit);
            this.Controls.Add(this.btnTheme);
            this.Controls.Add(this.btnReset);
            this.Controls.Add(this.btnStop);
            this.Name = "SinglePlayerForm";
            this.Text = "Single Player";
            this.ResumeLayout(false);
        }
    }
}