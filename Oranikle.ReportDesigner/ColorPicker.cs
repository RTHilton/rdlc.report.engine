/* ====================================================================
   

   
	
   
   
   

       

   
   
   
   
   


   
   
*/
using System;
using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;

namespace Oranikle.ReportDesigner
{
    public class ColorPicker : ComboBox
    { 
        private const int RECTCOLOR_LEFT = 4;
        private const int RECTCOLOR_TOP = 2;
        private const int RECTCOLOR_WIDTH = 10;
        private const int RECTTEXT_MARGIN = 5;
        private const int RECTTEXT_LEFT = RECTCOLOR_LEFT + RECTCOLOR_WIDTH + RECTTEXT_MARGIN;
        ColorPickerPopup _DropListBox;

        public ColorPicker()
        {
            this.DrawMode = DrawMode.OwnerDrawFixed;
            this.DropDownStyle = ComboBoxStyle.DropDownList; // DropDownList
            this.DropDownHeight = 1;
            this.Items.AddRange(StaticLists.ColorList);
            Font = new Font("Arial", 8, FontStyle.Bold | FontStyle.Italic);

            _DropListBox = new ColorPickerPopup(this);
        }
        public override string Text
        {
            get
            {
                return base.Text;
            }
            set
            {
                string v = value == null ? "" : value;
                if (!this.Items.Contains(v))    // make sure item is always in the list
                    this.Items.Add(v);
                base.Text = v;
            }
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            Graphics g = e.Graphics;
            Color BlockColor = Color.Empty;
            int left = RECTCOLOR_LEFT;
            if (e.State == DrawItemState.Selected || e.State == DrawItemState.None)
                e.DrawBackground();
            if (e.Index == -1)
            {
                BlockColor = SelectedIndex < 0 ? BackColor : DesignerUtility.ColorFromHtml(this.Text, Color.Empty);
            }
            else
                BlockColor = DesignerUtility.ColorFromHtml((string)this.Items[e.Index], Color.Empty);
            // Fill rectangle
            if (BlockColor.IsEmpty && this.Text.StartsWith("="))
            {
                g.DrawString("fx", this.Font, Brushes.Black, e.Bounds);
            }
            else
            {
                g.FillRectangle(new SolidBrush(BlockColor), left, e.Bounds.Top + RECTCOLOR_TOP, RECTCOLOR_WIDTH,
                    ItemHeight - 2 * RECTCOLOR_TOP);
            }
            base.OnDrawItem(e);
        }

        protected override void OnDropDown(System.EventArgs e)
        {
            _DropListBox.Location = this.PointToScreen(new Point(0, this.Height));
            _DropListBox.Show();
        }
    }
}
