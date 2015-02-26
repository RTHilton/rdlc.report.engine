/* ====================================================================
   

   
	
   
   
   

       

   
   
   
   
   


   
   
*/

using System;
using System.Xml;
using System.Collections;

namespace Oranikle.Report.Engine
{
	///<summary>
	/// In Matrix, the dynamic rows needed.
	///</summary>
	[Serializable]
	public class DynamicRows : ReportLink
	{
		Grouping _Grouping;	// The expressions to group the data by
		Sorting _Sorting;	// The expressions to sort the columns by
		Subtotal _Subtotal;	// Indicates an automatic subtotal row should be included
		ReportItems _ReportItems;	// The elements of the row header layout
							// This ReportItems collection must contain exactly one
							// ReportItem. The Top, Left, Height and Width for this
							// ReportItem are ignored. The position is taken to be 0,
							// 0 and the size to be 100%, 100%.
		Visibility _Visibility;	// Indicates if all of the dynamic rows for this grouping
							// should be hidden and replaced with a subtotal row for
							// this grouping scope		

		public DynamicRows(ReportDefn r, ReportLink p, XmlNode xNode) : base(r, p)
		{
			_Grouping=null;
			_Sorting=null;
			_Subtotal=null;
			_ReportItems=null;
			_Visibility=null;
			// Run thru the attributes
			//			foreach(XmlAttribute xAttr in xNode.Attributes)
			//			{
			//			}

			// Loop thru all the child nodes
			foreach(XmlNode xNodeLoop in xNode.ChildNodes)
			{
				if (xNodeLoop.NodeType != XmlNodeType.Element)
					continue;
				switch (xNodeLoop.Name)
				{
					case "Grouping":
						_Grouping = new Grouping(r, this, xNodeLoop);
						break;
					case "Sorting":
						_Sorting = new Sorting(r, this, xNodeLoop);
						break;
					case "Subtotal":
						_Subtotal = new Subtotal(r, this, xNodeLoop);
						break;
					case "ReportItems":
						_ReportItems = new ReportItems(r, this, xNodeLoop);
						break;
					case "Visibility":
						_Visibility = new Visibility(r, this, xNodeLoop);
						break;
					default:	
						// don't know this element - log it
						OwnerReport.rl.LogError(4, "Unknown DynamicRow element '" + xNodeLoop.Name + "' ignored.");
						break;
				}
			}
			if (_Grouping == null)
				OwnerReport.rl.LogError(8, "DynamicRows requires the Grouping element.");
			if (_ReportItems == null || _ReportItems.Items.Count != 1)
				OwnerReport.rl.LogError(8, "DynamicRows requires the ReportItems element defined with exactly one report item.");
		}
		
		override public void FinalPass()
		{
			if (_Grouping != null)
				_Grouping.FinalPass();
			if (_Sorting != null)
				_Sorting.FinalPass();
			if (_Subtotal != null)
				_Subtotal.FinalPass();
			if (_ReportItems != null)
				_ReportItems.FinalPass();
			if (_Visibility != null)
				_Visibility.FinalPass();
			return;
		}

		public Grouping Grouping
		{
			get { return  _Grouping; }
			set {  _Grouping = value; }
		}

		public Sorting Sorting
		{
			get { return  _Sorting; }
			set {  _Sorting = value; }
		}

		public Subtotal Subtotal
		{
			get { return  _Subtotal; }
			set {  _Subtotal = value; }
		}

		public ReportItems ReportItems
		{
			get { return  _ReportItems; }
			set {  _ReportItems = value; }
		}

		public Visibility Visibility
		{
			get { return  _Visibility; }
			set {  _Visibility = value; }
		}
	}
}
