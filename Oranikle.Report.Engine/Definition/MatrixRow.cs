/* ====================================================================
   

   
	
   
   
   

       

   
   
   
   
   


   
   
*/

using System;
using System.Xml;

namespace Oranikle.Report.Engine
{
	///<summary>
	/// Handle a Matrix Row: i.e. height and matrix cells that make up the row.
	///</summary>
	[Serializable]
	public class MatrixRow : ReportLink
	{
		RSize _Height;	// Height of each detail cell in this row.
		MatrixCells _MatrixCells;	// The set of cells in a row in the detail section of the Matrix.		
	
		public MatrixRow(ReportDefn r, ReportLink p, XmlNode xNode) : base(r, p)
		{
			_Height=null;
			_MatrixCells=null;

			// Loop thru all the child nodes
			foreach(XmlNode xNodeLoop in xNode.ChildNodes)
			{
				if (xNodeLoop.NodeType != XmlNodeType.Element)
					continue;
				switch (xNodeLoop.Name)
				{
					case "Height":
						_Height = new RSize(r, xNodeLoop);
						break;
					case "MatrixCells":
						_MatrixCells = new MatrixCells(r, this, xNodeLoop);
						break;
					default:
						break;
				}
			}
			if (_MatrixCells == null)
				OwnerReport.rl.LogError(8, "MatrixRow requires the MatrixCells element.");
		}
		
		override public void FinalPass()
		{
			if (_MatrixCells != null)
				_MatrixCells.FinalPass();
			return;
		}

		public RSize Height
		{
			get { return  _Height; }
			set {  _Height = value; }
		}

		public MatrixCells MatrixCells
		{
			get { return  _MatrixCells; }
			set {  _MatrixCells = value; }
		}
	}
}
