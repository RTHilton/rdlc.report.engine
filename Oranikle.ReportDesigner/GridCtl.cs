/* ====================================================================
   

   
	
   
   
   

       

   
   
   
   
   


   
   
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Windows.Forms;
using System.Xml;

namespace Oranikle.ReportDesigner
{
	/// <summary>
	/// Summary description for ReportCtl.
	/// </summary>
	internal class GridCtl : Oranikle.ReportDesigner.Base.BaseControl, IProperty
	{
        private List<XmlNode> _ReportItems;
		private DesignXmlDraw _Draw;
		bool fPBBefore, fPBAfter;
        bool fCheckRows;
		private System.Windows.Forms.GroupBox groupBox1;
		private Oranikle.Studio.Controls.StyledCheckBox chkPBBefore;
        private Oranikle.Studio.Controls.StyledCheckBox chkPBAfter;
		private System.Windows.Forms.GroupBox groupBox3;
		private Oranikle.Studio.Controls.StyledCheckBox chkDetails;
		private Oranikle.Studio.Controls.StyledCheckBox chkHeaderRows;
        private Oranikle.Studio.Controls.StyledCheckBox chkFooterRows;
        private Oranikle.Studio.Controls.StyledCheckBox chkFooterRepeat;
        private Oranikle.Studio.Controls.StyledCheckBox chkHeaderRepeat;
		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

        internal GridCtl(DesignXmlDraw dxDraw, List<XmlNode> ris)
		{
			_ReportItems = ris;
			_Draw = dxDraw;
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();

			// Initialize form using the style node values
			InitValues();			
		}

		private void InitValues()
		{
			XmlNode riNode = _ReportItems[0];

			chkPBBefore.Checked = _Draw.GetElementValue(riNode, "PageBreakAtStart", "false").ToLower()=="true"? true:false;
			chkPBAfter.Checked = _Draw.GetElementValue(riNode, "PageBreakAtEnd", "false").ToLower()=="true"? true:false;

			this.chkDetails.Checked = _Draw.GetNamedChildNode(riNode, "Details") != null;
            XmlNode fNode = _Draw.GetNamedChildNode(riNode, "Footer");
			this.chkFooterRows.Checked = fNode != null;
            if (fNode != null)
            {
                chkFooterRepeat.Checked = _Draw.GetElementValue(fNode, "RepeatOnNewPage", "false").ToLower() == "true" ? true : false;
            }
            else
                chkFooterRepeat.Enabled = false;

            XmlNode hNode = _Draw.GetNamedChildNode(riNode, "Header");
            this.chkHeaderRows.Checked = hNode != null;
            if (hNode != null)
            {
                chkHeaderRepeat.Checked = _Draw.GetElementValue(hNode, "RepeatOnNewPage", "false").ToLower() == "true" ? true : false;
            }
            else
                chkHeaderRepeat.Enabled = false;

			fPBBefore = fPBAfter = fCheckRows = false;
		}

		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Component Designer generated code
		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.chkPBAfter = new Oranikle.Studio.Controls.StyledCheckBox();
            this.chkPBBefore = new Oranikle.Studio.Controls.StyledCheckBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.chkFooterRepeat = new Oranikle.Studio.Controls.StyledCheckBox();
            this.chkHeaderRepeat = new Oranikle.Studio.Controls.StyledCheckBox();
            this.chkFooterRows = new Oranikle.Studio.Controls.StyledCheckBox();
            this.chkHeaderRows = new Oranikle.Studio.Controls.StyledCheckBox();
            this.chkDetails = new Oranikle.Studio.Controls.StyledCheckBox();
            this.groupBox1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.chkPBAfter);
            this.groupBox1.Controls.Add(this.chkPBBefore);
            this.groupBox1.Location = new System.Drawing.Point(24, 22);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(400, 48);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Page Breaks";
            // 
            // chkPBAfter
            // 
            this.chkPBAfter.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkPBAfter.ForeColor = System.Drawing.Color.Black;
            this.chkPBAfter.Location = new System.Drawing.Point(192, 16);
            this.chkPBAfter.Name = "chkPBAfter";
            this.chkPBAfter.Size = new System.Drawing.Size(128, 24);
            this.chkPBAfter.TabIndex = 1;
            this.chkPBAfter.Text = "Insert after Grid";
            this.chkPBAfter.CheckedChanged += new System.EventHandler(this.chkPBAfter_CheckedChanged);
            // 
            // chkPBBefore
            // 
            this.chkPBBefore.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkPBBefore.ForeColor = System.Drawing.Color.Black;
            this.chkPBBefore.Location = new System.Drawing.Point(16, 16);
            this.chkPBBefore.Name = "chkPBBefore";
            this.chkPBBefore.Size = new System.Drawing.Size(128, 24);
            this.chkPBBefore.TabIndex = 0;
            this.chkPBBefore.Text = "Insert before Grid";
            this.chkPBBefore.CheckedChanged += new System.EventHandler(this.chkPBBefore_CheckedChanged);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.chkFooterRepeat);
            this.groupBox3.Controls.Add(this.chkHeaderRepeat);
            this.groupBox3.Controls.Add(this.chkFooterRows);
            this.groupBox3.Controls.Add(this.chkHeaderRows);
            this.groupBox3.Controls.Add(this.chkDetails);
            this.groupBox3.Location = new System.Drawing.Point(24, 86);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(400, 70);
            this.groupBox3.TabIndex = 3;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Include Grid Rows";
            // 
            // chkFooterRepeat
            // 
            this.chkFooterRepeat.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkFooterRepeat.ForeColor = System.Drawing.Color.Black;
            this.chkFooterRepeat.Location = new System.Drawing.Point(272, 34);
            this.chkFooterRepeat.Name = "chkFooterRepeat";
            this.chkFooterRepeat.Size = new System.Drawing.Size(122, 30);
            this.chkFooterRepeat.TabIndex = 4;
            this.chkFooterRepeat.Text = "Repeat footer on new page";
            this.chkFooterRepeat.CheckedChanged += new System.EventHandler(this.chkRows_CheckedChanged);
            // 
            // chkHeaderRepeat
            // 
            this.chkHeaderRepeat.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkHeaderRepeat.ForeColor = System.Drawing.Color.Black;
            this.chkHeaderRepeat.Location = new System.Drawing.Point(144, 34);
            this.chkHeaderRepeat.Name = "chkHeaderRepeat";
            this.chkHeaderRepeat.Size = new System.Drawing.Size(122, 30);
            this.chkHeaderRepeat.TabIndex = 3;
            this.chkHeaderRepeat.Text = "Repeat header on new page";
            this.chkHeaderRepeat.CheckedChanged += new System.EventHandler(this.chkRows_CheckedChanged);
            // 
            // chkFooterRows
            // 
            this.chkFooterRows.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkFooterRows.ForeColor = System.Drawing.Color.Black;
            this.chkFooterRows.Location = new System.Drawing.Point(272, 13);
            this.chkFooterRows.Name = "chkFooterRows";
            this.chkFooterRows.Size = new System.Drawing.Size(104, 24);
            this.chkFooterRows.TabIndex = 2;
            this.chkFooterRows.Text = "Footer Rows";
            this.chkFooterRows.CheckedChanged += new System.EventHandler(this.chkRows_CheckedChanged);
            // 
            // chkHeaderRows
            // 
            this.chkHeaderRows.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkHeaderRows.ForeColor = System.Drawing.Color.Black;
            this.chkHeaderRows.Location = new System.Drawing.Point(144, 13);
            this.chkHeaderRows.Name = "chkHeaderRows";
            this.chkHeaderRows.Size = new System.Drawing.Size(104, 24);
            this.chkHeaderRows.TabIndex = 1;
            this.chkHeaderRows.Text = "Header Rows";
            this.chkHeaderRows.CheckedChanged += new System.EventHandler(this.chkRows_CheckedChanged);
            // 
            // chkDetails
            // 
            this.chkDetails.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkDetails.ForeColor = System.Drawing.Color.Black;
            this.chkDetails.Location = new System.Drawing.Point(16, 13);
            this.chkDetails.Name = "chkDetails";
            this.chkDetails.Size = new System.Drawing.Size(104, 24);
            this.chkDetails.TabIndex = 1;
            this.chkDetails.Text = "Detail Rows";
            this.chkDetails.CheckedChanged += new System.EventHandler(this.chkRows_CheckedChanged);
            // 
            // GridCtl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox1);
            this.Name = "GridCtl";
            this.Size = new System.Drawing.Size(472, 288);
            this.groupBox1.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.ResumeLayout(false);

		}
		#endregion
  
		public bool IsValid()
		{
			if (this.chkDetails.Checked || this.chkFooterRows.Checked || this.chkHeaderRows.Checked)
				return true;

			MessageBox.Show("Grid must have at least one Header, Details or Footer row defined.", "Grid");

			return false;
		}

		public void Apply()
		{
			// take information in control and apply to all the style nodes
			//  Only change information that has been marked as modified;
			//   this way when group is selected it is possible to change just
			//   the items you want and keep the rest the same.
				
			foreach (XmlNode riNode in this._ReportItems)
				ApplyChanges(riNode);

			// No more changes
			fPBBefore = fPBAfter = fCheckRows = false;
		}

		public void ApplyChanges(XmlNode node)
		{
			if (fPBBefore)
				_Draw.SetElement(node, "PageBreakAtStart", this.chkPBBefore.Checked? "true":"false");
			if (fPBAfter)
				_Draw.SetElement(node, "PageBreakAtEnd", this.chkPBAfter.Checked? "true":"false");
			if (fCheckRows)
			{
				if (this.chkDetails.Checked)
					CreateTableRow(node, "Details", false);
				else
					_Draw.RemoveElement(node, "Details");
				if (this.chkHeaderRows.Checked)
					CreateTableRow(node, "Header", chkHeaderRepeat.Checked);
				else
					_Draw.RemoveElement(node, "Header");
				if (this.chkFooterRows.Checked)
					CreateTableRow(node, "Footer", chkFooterRepeat.Checked);
				else
					_Draw.RemoveElement(node, "Footer");
			}
		}

		private void CreateTableRow(XmlNode tblNode, string elementName, bool bRepeatOnNewPage)
		{
			XmlNode node = _Draw.GetNamedChildNode(tblNode, elementName);
			if (node == null)
			{
				node = _Draw.CreateElement(tblNode, elementName, null);
				XmlNode tblRows = _Draw.CreateElement(node, "TableRows", null);
				_Draw.InsertTableRow(tblRows);
			}
            if (bRepeatOnNewPage)
                _Draw.SetElement(node, "RepeatOnNewPage", "true");
            else
                _Draw.RemoveElement(node, "RepeatOnNewPage");

			return;
		}

		private void chkPBBefore_CheckedChanged(object sender, System.EventArgs e)
		{
			fPBBefore = true;
		}

		private void chkPBAfter_CheckedChanged(object sender, System.EventArgs e)
		{
			fPBAfter = true;
		}

		private void chkRows_CheckedChanged(object sender, System.EventArgs e)
		{
			this.fCheckRows = true;
            chkFooterRepeat.Enabled = chkFooterRows.Checked;
            chkHeaderRepeat.Enabled = chkHeaderRows.Checked;
		}

	}
}
