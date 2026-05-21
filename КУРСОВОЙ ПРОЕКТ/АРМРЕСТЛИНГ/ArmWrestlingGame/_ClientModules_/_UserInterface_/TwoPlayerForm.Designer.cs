namespace ArmWrestlingGame
{
    partial class TwoPlayerForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button btnTheme;
        private System.Windows.Forms.Button btnExit;
        private System.Windows.Forms.Button btnSearch;
        private System.Windows.Forms.Button btnRematch;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.btnTheme = new System.Windows.Forms.Button();
            this.btnExit = new System.Windows.Forms.Button();
            this.btnSearch = new System.Windows.Forms.Button();
            this.btnRematch = new System.Windows.Forms.Button();
            this.SuspendLayout();

            int btnWidth = 180;
            int btnHeight = 50;
            int startY = 30;
            int gap = 70;

            // btnTheme
            this.btnTheme.Location = new System.Drawing.Point(30, startY);
            this.btnTheme.Name = "btnTheme";
            this.btnTheme.Size = new System.Drawing.Size(btnWidth, btnHeight);
            this.btnTheme.TabIndex = 0;
            this.btnTheme.TabStop = false;
            this.btnTheme.Text = "Theme";
            this.btnTheme.Font = new System.Drawing.Font("Consolas", 14F, System.Drawing.FontStyle.Bold);
            this.btnTheme.UseVisualStyleBackColor = false;
            this.btnTheme.BackColor = System.Drawing.Color.FromArgb(50, 50, 50);
            this.btnTheme.ForeColor = System.Drawing.Color.White;
            this.btnTheme.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnTheme.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnTheme.Click += new System.EventHandler(this.BtnTheme_Click);

            // btnExit
            this.btnExit.Location = new System.Drawing.Point(30, startY + gap);
            this.btnExit.Name = "btnExit";
            this.btnExit.Size = new System.Drawing.Size(btnWidth, btnHeight);
            this.btnExit.TabIndex = 1;
            this.btnExit.TabStop = false;
            this.btnExit.Text = "Exit";
            this.btnExit.Font = new System.Drawing.Font("Consolas", 14F, System.Drawing.FontStyle.Bold);
            this.btnExit.UseVisualStyleBackColor = false;
            this.btnExit.BackColor = System.Drawing.Color.FromArgb(50, 50, 50);
            this.btnExit.ForeColor = System.Drawing.Color.White;
            this.btnExit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExit.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnExit.Click += new System.EventHandler(this.BtnExit_Click);

            // ОГРОМНАЯ КНОПКА ПОИСКА ПО ЦЕНТРУ
            this.btnSearch.Anchor = System.Windows.Forms.AnchorStyles.None;
            // Размер 600x150, смещение для центрирования относительно начального размера формы
            this.btnSearch.Location = new System.Drawing.Point((1920 - 600) / 2, (1080 - 150) / 2); // Базовый расчет под Full HD
            this.btnSearch.Name = "btnSearch";
            this.btnSearch.Size = new System.Drawing.Size(600, 150);
            this.btnSearch.TabIndex = 2;
            this.btnSearch.TabStop = false;
            this.btnSearch.Text = "FIND MATCH";
            this.btnSearch.Font = new System.Drawing.Font("Consolas", 42F, System.Drawing.FontStyle.Bold);
            this.btnSearch.UseVisualStyleBackColor = false;
            this.btnSearch.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            this.btnSearch.ForeColor = System.Drawing.Color.LimeGreen;
            this.btnSearch.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSearch.FlatAppearance.BorderColor = System.Drawing.Color.LimeGreen;
            this.btnSearch.FlatAppearance.BorderSize = 5;
            this.btnSearch.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSearch.Click += new System.EventHandler(this.BtnSearch_Click);

            // Настройки btnRematch (такого же размера как поиск, скрыта по умолчанию)
            this.btnRematch.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btnRematch.Location = new System.Drawing.Point((1920 - 600) / 2, (1080 - 150) / 2);
            this.btnRematch.Name = "btnRematch";
            this.btnRematch.Size = new System.Drawing.Size(600, 150);
            this.btnRematch.TabIndex = 3;
            this.btnRematch.TabStop = false;
            this.btnRematch.Text = "REMATCH";
            this.btnRematch.Font = new System.Drawing.Font("Consolas", 42F, System.Drawing.FontStyle.Bold);
            this.btnRematch.UseVisualStyleBackColor = false;
            this.btnRematch.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            this.btnRematch.ForeColor = System.Drawing.Color.Gold;
            this.btnRematch.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRematch.FlatAppearance.BorderColor = System.Drawing.Color.Gold;
            this.btnRematch.FlatAppearance.BorderSize = 5;
            this.btnRematch.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRematch.Visible = false; // Скрыта до окончания матча
            this.btnRematch.Click += new System.EventHandler(this.BtnRematch_Click);

            // Form Configurations
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1920, 1080); // Установил дефолт под FHD для якоря
            this.Controls.Add(this.btnSearch);
            this.Controls.Add(this.btnExit);
            this.Controls.Add(this.btnTheme);
            this.Controls.Add(this.btnRematch);
            this.Name = "TwoPlayerForm";
            this.Text = "TwoPlayerForm";
            this.ResumeLayout(false);
        }

        #endregion
    }
}