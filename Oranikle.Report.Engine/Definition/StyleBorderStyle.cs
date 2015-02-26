/* ====================================================================
   

   
	
   
   
   

       

   
   
   
   
   


   
   
*/

using System;
using System.Xml;
using System.Text;

namespace Oranikle.Report.Engine
{
	///<summary>
	/// The type (dotted, solid, ...) of border.  Expressions for all sides as well as default expression.
	///</summary>
	[Serializable]
	public class StyleBorderStyle : ReportLink
	{
		Expression _Default;	// (Enum BorderStyle) Style of the border (unless overridden for a specific side)
		// Default: none		
		Expression _Left;		// (Enum BorderStyle) Style of the left border
		Expression _Right;		// (Enum BorderStyle) Style of the right border
		Expression _Top;		// (Enum BorderStyle) Style of the top border
		Expression _Bottom;		// (Enum BorderStyle) Style of the bottom border
	
		public StyleBorderStyle(ReportDefn r, ReportLink p, XmlNode xNode) : base(r, p)
		{
			_Default=null;
			_Left=null;
			_Right=null;
			_Top=null;
			_Bottom=null;

			// Loop thru all the child nodes
			foreach(XmlNode xNodeLoop in xNode.ChildNodes)
			{
				if (xNodeLoop.NodeType != XmlNodeType.Element)
					continue;
				switch (xNodeLoop.Name)
				{
					case "Default":
						_Default = new Expression(r, this, xNodeLoop, ExpressionType.Enum);
						break;
					case "Left":
						_Left = new Expression(r, this, xNodeLoop, ExpressionType.Enum);
						break;
					case "Right":
						_Right = new Expression(r, this, xNodeLoop, ExpressionType.Enum);
						break;
					case "Top":
						_Top = new Expression(r, this, xNodeLoop, ExpressionType.Enum);
						break;
					case "Bottom":
						_Bottom = new Expression(r, this, xNodeLoop, ExpressionType.Enum);
						break;
					default:	
						// don't know this element - log it
						OwnerReport.rl.LogError(4, "Unknown BorderStyle element '" + xNodeLoop.Name + "' ignored.");
						break;
				}
			}
		}

		// Handle parsing of function in final pass
		override public void FinalPass()
		{
			if (_Default != null)
				_Default.FinalPass();
			if (_Left != null)
				_Left.FinalPass();
			if (_Right != null)
				_Right.FinalPass();
			if (_Top != null)
				_Top.FinalPass();
			if (_Bottom != null)
				_Bottom.FinalPass();
			return;
		}

		// Generate a CSS string from the specified styles
		public string GetCSS(Report rpt, Row row, bool bDefaults)
		{
			StringBuilder sb = new StringBuilder();

			if (_Default != null)
				sb.AppendFormat("border-style:{0};",_Default.EvaluateString(rpt, row));
			else if (bDefaults)
				sb.Append("border-style:none;");

			if (_Left != null)
				sb.AppendFormat("border-left-style:{0};",_Left.EvaluateString(rpt, row));

			if (_Right != null)
				sb.AppendFormat("border-right-style:{0};",_Right.EvaluateString(rpt, row));

			if (_Top != null)
				sb.AppendFormat("border-top-style:{0};",_Top.EvaluateString(rpt, row));

			if (_Bottom != null)
				sb.AppendFormat("border-bottom-style:{0};",_Bottom.EvaluateString(rpt, row));

			return sb.ToString();
		}

		public bool IsConstant()
		{
			bool rc = true;

			if (_Default != null)
				rc = _Default.IsConstant();

			if (!rc)
				return false;

			if (_Left != null)
				rc = _Left.IsConstant();

			if (!rc)
				return false;

			if (_Right != null)
				rc = _Right.IsConstant();

			if (!rc)
				return false;

			if (_Top != null)
				rc = _Top.IsConstant();

			if (!rc)
				return false;

			if (_Bottom != null)
				rc = _Bottom.IsConstant();

			return rc;
		}

		static public string GetCSSDefaults()
		{
			return "border-style:none;";
		}

		public Expression Default
		{
			get { return  _Default; }
			set {  _Default = value; }
		}
 
		public BorderStyleEnum EvalDefault(Report rpt, Row r)
		{
			if (_Default == null)
				return BorderStyleEnum.None;

			string bs = _Default.EvaluateString(rpt, r);
			return GetBorderStyle(bs, BorderStyleEnum.Solid);
		}

		public Expression Left
		{
			get { return  _Left; }
			set {  _Left = value; }
		}

		public BorderStyleEnum EvalLeft(Report rpt, Row r)
		{
			if (_Left == null)
				return EvalDefault(rpt, r);

			string bs = _Left.EvaluateString(rpt, r);
			return GetBorderStyle(bs, BorderStyleEnum.Solid);
		}

		public Expression Right
		{
			get { return  _Right; }
			set {  _Right = value; }
		}

		public BorderStyleEnum EvalRight(Report rpt, Row r)
		{
			if (_Right == null)
				return EvalDefault(rpt, r);

			string bs = _Right.EvaluateString(rpt, r);
			return GetBorderStyle(bs, BorderStyleEnum.Solid);
		}

		public Expression Top
		{
			get { return  _Top; }
			set {  _Top = value; }
		}

		public BorderStyleEnum EvalTop(Report rpt, Row r)
		{
			if (_Top == null)
				return EvalDefault(rpt, r);

			string bs = _Top.EvaluateString(rpt, r);
			return GetBorderStyle(bs, BorderStyleEnum.Solid);
		}

		public Expression Bottom
		{
			get { return  _Bottom; }
			set {  _Bottom = value; }
		}

		public BorderStyleEnum EvalBottom(Report rpt, Row r)
		{
			if (_Bottom == null)
				return EvalDefault(rpt, r);

			string bs = _Bottom.EvaluateString(rpt, r);
			return GetBorderStyle(bs, BorderStyleEnum.Solid);
		}

		// return the BorderStyleEnum given a particular string value
		static public BorderStyleEnum GetBorderStyle(string v, BorderStyleEnum def)
		{
			BorderStyleEnum bs;
			switch (v)
			{
				case "None":
					bs = BorderStyleEnum.None;
					break;
				case "Dotted":
					bs = BorderStyleEnum.Dotted;
					break;
				case "Dashed":
					bs = BorderStyleEnum.Dashed;
					break;
				case "Solid":
					bs = BorderStyleEnum.Solid;
					break;
				case "Double":
					bs = BorderStyleEnum.Double;
					break;
				case "Groove":
					bs = BorderStyleEnum.Groove;
					break;
				case "Ridge":
					bs = BorderStyleEnum.Ridge;
					break;
				case "Inset":
					bs = BorderStyleEnum.Inset;
					break;
				case "WindowInset":
					bs = BorderStyleEnum.WindowInset;
					break;
				case "Outset":
					bs = BorderStyleEnum.Outset;
					break;
				default:
					bs = def;
					break;
			}
			return bs;
		}
	}
	/// <summary>
	/// Allowed values for border styles.  Note: these may not be actually supported depending
	/// on the renderer used.
	/// </summary>
	public enum BorderStyleEnum
	{
		/// <summary>
		/// No border
		/// </summary>
		None,
		/// <summary>
		/// Dotted line border
		/// </summary>
		Dotted,
		/// <summary>
		/// Dashed lin border
		/// </summary>
		Dashed,
		/// <summary>
		/// Solid line border
		/// </summary>
		Solid,
		/// <summary>
		/// Double line border
		/// </summary>
		Double,
		/// <summary>
		/// Grooved border
		/// </summary>
		Groove,
		/// <summary>
		/// Ridge border
		/// </summary>
		Ridge,
		/// <summary>
		/// Inset border
		/// </summary>
		Inset,
		/// <summary>
		/// Windows Inset border
		/// </summary>
		WindowInset,
		/// <summary>
		/// Outset border
		/// </summary>
		Outset
	}
}