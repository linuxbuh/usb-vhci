namespace VhciGui
{
	partial class MainForm
	{
		private System.ComponentModel.IContainer components = null;

		protected override void Dispose(bool disposing)
		{
			if(disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows form designer generated code

		private void InitializeComponent()
		{
			this.mainMenu = new System.Windows.Forms.MenuStrip();
			this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.closeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.rescanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.statusBar = new System.Windows.Forms.StatusStrip();
			this.split = new System.Windows.Forms.SplitContainer();
			this.tree = new System.Windows.Forms.TreeView();
			this.view = new System.Windows.Forms.ListView();
			this.deviceMenu = new System.Windows.Forms.ContextMenuStrip();
			this.acquireToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.probeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.mainMenu.SuspendLayout();
			this.split.Panel1.SuspendLayout();
			this.split.Panel2.SuspendLayout();
			this.split.SuspendLayout();
			this.deviceMenu.SuspendLayout();
			this.SuspendLayout();
			// 
			// mainMenu
			// 
			this.mainMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem});
			this.mainMenu.Location = new System.Drawing.Point(0, 0);
			this.mainMenu.Name = "mainMenu";
			this.mainMenu.Size = new System.Drawing.Size(501, 24);
			this.mainMenu.TabIndex = 0;
			this.mainMenu.Text = "mainMenu";
			// 
			// fileToolStripMenuItem
			// 
			this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.rescanToolStripMenuItem,
            this.closeToolStripMenuItem});
			this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
			this.fileToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
			this.fileToolStripMenuItem.Text = "&File";
			// 
			// closeToolStripMenuItem
			// 
			this.closeToolStripMenuItem.Name = "closeToolStripMenuItem";
			this.closeToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.F4)));
			this.closeToolStripMenuItem.Size = new System.Drawing.Size(140, 22);
			this.closeToolStripMenuItem.Text = "&Close";
			this.closeToolStripMenuItem.Click += new System.EventHandler(this.closeToolStripMenuItem_Click);
			// 
			// rescanToolStripMenuItem
			// 
			this.rescanToolStripMenuItem.Name = "rescanToolStripMenuItem";
			this.rescanToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R)));
			this.rescanToolStripMenuItem.Size = new System.Drawing.Size(140, 22);
			this.rescanToolStripMenuItem.Text = "&Rescan Bus";
			this.rescanToolStripMenuItem.Click += new System.EventHandler(this.rescanToolStripMenuItem_Click);
			// 
			// statusBar
			// 
			this.statusBar.Location = new System.Drawing.Point(0, 303);
			this.statusBar.Name = "statusBar";
			this.statusBar.Size = new System.Drawing.Size(501, 22);
			this.statusBar.TabIndex = 1;
			this.statusBar.Text = "statusBar";
			// 
			// split
			// 
			this.split.Dock = System.Windows.Forms.DockStyle.Fill;
			this.split.Location = new System.Drawing.Point(0, 24);
			this.split.Name = "split";
			// 
			// split.Panel1
			// 
			this.split.Panel1.Controls.Add(this.tree);
			// 
			// split.Panel2
			// 
			this.split.Panel2.Controls.Add(this.view);
			this.split.Size = new System.Drawing.Size(501, 279);
			this.split.SplitterDistance = 202;
			this.split.TabIndex = 2;
			// 
			// tree
			// 
			this.tree.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tree.HideSelection = false;
			this.tree.Location = new System.Drawing.Point(0, 0);
			this.tree.Name = "tree";
			this.tree.Size = new System.Drawing.Size(202, 279);
			this.tree.TabIndex = 0;
			this.tree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.tree_AfterSelect);
			this.tree.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.tree_NodeMouseClick);
			this.tree.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.tree_NodeMouseDoubleClick);
			// 
			// view
			// 
			this.view.Dock = System.Windows.Forms.DockStyle.Fill;
			this.view.FullRowSelect = true;
			this.view.GridLines = true;
			this.view.Location = new System.Drawing.Point(0, 0);
			this.view.Name = "view";
			this.view.Size = new System.Drawing.Size(295, 279);
			this.view.TabIndex = 0;
			this.view.UseCompatibleStateImageBehavior = false;
			this.view.View = System.Windows.Forms.View.Details;
			// 
			// deviceMenu
			// 
			this.deviceMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.acquireToolStripMenuItem,
            this.probeToolStripMenuItem});
			this.deviceMenu.Location = new System.Drawing.Point(0, 0);
			this.deviceMenu.Name = "deviceMenu";
			this.deviceMenu.Size = new System.Drawing.Size(501, 24);
			this.deviceMenu.Text = "deviceMenu";
			// 
			// acquireToolStripMenuItem
			// 
			this.acquireToolStripMenuItem.Name = "acquireToolStripMenuItem";
			this.acquireToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
			this.acquireToolStripMenuItem.Text = "&Acquire";
			this.acquireToolStripMenuItem.Click += new System.EventHandler(this.acquireToolStripMenuItem_Click);
			// 
			// probeToolStripMenuItem
			// 
			this.probeToolStripMenuItem.Name = "probeToolStripMenuItem";
			this.probeToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
			this.probeToolStripMenuItem.Text = "Trigger Driver &Probe";
			this.probeToolStripMenuItem.Click += new System.EventHandler(this.probeToolStripMenuItem_Click);
			// 
			// MainForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(501, 325);
			this.Controls.Add(this.split);
			this.Controls.Add(this.statusBar);
			this.Controls.Add(this.mainMenu);
			this.MainMenuStrip = this.mainMenu;
			this.Name = "MainForm";
			this.Text = "VhciGui";
			this.mainMenu.ResumeLayout(false);
			this.mainMenu.PerformLayout();
			this.split.Panel1.ResumeLayout(false);
			this.split.Panel2.ResumeLayout(false);
			this.split.ResumeLayout(false);
			this.deviceMenu.ResumeLayout(false);
			this.deviceMenu.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.MenuStrip mainMenu;
		private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem closeToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem rescanToolStripMenuItem;
		private System.Windows.Forms.StatusStrip statusBar;
		private System.Windows.Forms.SplitContainer split;
		private System.Windows.Forms.TreeView tree;
		private System.Windows.Forms.ListView view;
		private System.Windows.Forms.ContextMenuStrip deviceMenu;
		private System.Windows.Forms.ToolStripMenuItem acquireToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem probeToolStripMenuItem;
	}
}

