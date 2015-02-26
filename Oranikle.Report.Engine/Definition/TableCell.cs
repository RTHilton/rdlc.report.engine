/* ====================================================================
   

   
	
   
   
   

       

   
   
   
   
   


   
   
*/

using System;
using System.Xml;
using System.IO;

namespace Oranikle.Report.Engine
{
	///<summary>
	/// TableCell definition and processing.
	///</summary>
	[Serializable]
	public class TableCell : ReportLink
	{
		ReportItems _ReportItems;	// An element of the report layout (e.g. List, Textbox,
									// Line). This ReportItems collection must contain
									// exactly one ReportItem. The Top, Left, Height and
									// Width for this ReportItem are ignored. The
									// position is taken to be 0, 0 and the size to be 100%,
									// 100%. Pagebreaks on report items inside a
									// TableCell are ignored.
		int _ColSpan;	// Indicates the number of columns this cell spans.1
						// A ColSpan of 1 is the same as not specifying a ColSpan	

		// some bookkeeping fields
		Table _OwnerTable;			// Table that owns this column
		int _ColIndex;				// Column number within table; used for
									//    xrefing with other parts of table columns; e.g. column headers with details
		bool _InTableHeader;		// true if tablecell is part of header; simplifies HTML processing
		bool _InTableFooter;		// true if tablecell is part of footer; simplifies HTML processing
	
		public TableCell(ReportDefn r, ReportLink p, XmlNode xNode, int colIndex) : base(r, p)
		{
			_ColIndex = colIndex;
			_ReportItems=null;
			_ColSpan=1;

			// Loop thru all the child nodes
			foreach(XmlNode xNodeLoop in xNode.ChildNodes)
			{
				if (xNodeLoop.NodeType != XmlNodeType.Element)
					continue;
				switch (xNodeLoop.Name)
				{
					case "ReportItems":
						_ReportItems = new ReportItems(r, this, xNodeLoop);
						break;
					case "ColSpan":
						_ColSpan = XmlUtil.Integer(xNodeLoop.InnerText);
						break;
					default:
						// don't know this element - log it
						OwnerReport.rl.LogError(4, "Unknown TableCell element '" + xNodeLoop.Name + "' ignored.");
						break;
				}
			}
			// Must have exactly one ReportItems
			if (_ReportItems == null)
				OwnerReport.rl.LogError(8, "ReportItems element is required with a TableCell but not specified.");
			else if (_ReportItems.Items.Count != 1)
				OwnerReport.rl.LogError(8, "Only one element in ReportItems element is allowed within a TableCell.");

			// Obtain the tablecell's owner table;
			//		determine if tablecell is part of table header
			_InTableHeader = false;
			ReportLink rl;
			for (rl=this.Parent; rl != null; rl=rl.Parent)
			{
				if (rl is Table)
				{
					_OwnerTable = (Table) rl;
					break;
				}
				
				if (rl is Header && rl.Parent is Table)	// Header and parent is Table (not TableGroup)
				{
					_InTableHeader=true;
				}
				
				if (rl is Footer && rl.Parent is Table)	// Header and parent is Table (not TableGroup)
				{
					_InTableFooter=true;
				}
			}
			return;
		}
		
		override public void FinalPass()
		{
			_ReportItems.FinalPass();
			return;
		}

		public void Run(IPresent ip, Row row)
		{
			// todo: visibility on the column should really only be evaluated once at the beginning
			//   of the table processing;  also this doesn't account for the affect of colspan correctly
			//   where if any of the spanned columns are visible the value would show??
			TableColumn tc = _OwnerTable.TableColumns[_ColIndex];
			if (tc.Visibility != null && tc.Visibility.IsHidden(ip.Report(), row))	// column visible?
				return;													//  no nothing to do

			ip.TableCellStart(this, row);
			
			_ReportItems.Items[0].Run(ip, row);

			ip.TableCellEnd(this, row);
			return;
		}

		public void RunPage(Pages pgs, Row row)
		{
			// todo: visibility on the column should really only be evaluated once at the beginning
			//   of the table processing;  also this doesn't account for the affect of colspan correctly
			//   where if any of the spanned columns are visible the value would show??
			TableColumn tc = _OwnerTable.TableColumns[_ColIndex];
			if (tc.Visibility != null && tc.Visibility.IsHidden(pgs.Report, row))	// column visible?
				return;													//  no nothing to do

			_ReportItems.Items[0].RunPage(pgs, row);
			return;
		}

		public ReportItems ReportItems
		{
			get { return  _ReportItems; }
			set {  _ReportItems = value; }
		}

		public Table OwnerTable
		{
			get { return _OwnerTable; }
		}

		public int ColSpan
		{
			get { return  _ColSpan; }
			set {  _ColSpan = value; }
		}

		public int ColIndex
		{
			get { return  _ColIndex; }
		}

		public bool InTableFooter
		{
			get { return  _InTableFooter; }
		}

		public bool InTableHeader
		{
			get { return  _InTableHeader; }
		}
	}
}
