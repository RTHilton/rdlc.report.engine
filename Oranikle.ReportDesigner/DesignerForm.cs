﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Oranikle.Report.Engine;
using System.Diagnostics;
using System.Xml;
using System.Runtime.InteropServices;
using System.IO;
using System.Globalization;
using System.Drawing.Printing;
using Oranikle.Report.Viewer;

namespace Oranikle.ReportDesigner
{
    public partial class DesignerForm : Form, IMessageFilter
    {
        static readonly string IpcFileName = @"\fyiIpcData400.txt"; // note: change file name with every release
        Timer _IpcTimer = null;

        private MDIChild printChild = null;
        SortedList<DateTime, string> _RecentFiles = null;
        List<string> _CurrentFiles = null;		// temporary variable for current files
        List<string> _Toolbar = null;			// temporary variable for toolbar entries
        List<string> _TempReportFiles = null;		// temporary files created for report browsing
        int _RecentFilesMax = 5;			// # of items in recent files
        Process _ServerProcess = null;		// process for the RdlDesktop.exe --
        private NeedPassword _GetPassword;
        private string _DataSourceReferencePassword = null;
        private bool bGotPassword = false;
        private bool bMono = DesignerUtility.IsMono();
        private readonly string DefaultHelpUrl = "http://www.oranikle.in";
        private readonly string DefaultSupportUrl = "http://www.oranikle.in";
        private string _HelpUrl;
        private string _SupportUrl;
        static private string[] _MapSubtypes = new string[] { "usa_map" };
        private DialogValidateRdl _ValidateRdl = null;
        private bool _ShowPreviewWaitDialog = true;
        private bool _ShowEditLines = true;
        private bool _ShowReportItemOutline = false;
        private bool _ShowTabbedInterface = true;
        private bool _ShowProperties = true;
        private DockStyle _PropertiesLocation = DockStyle.Right;
        private bool _PropertiesAutoHide = true;
        private readonly string TEMPRDL = "_tempfile_.rdlc";
        private int TEMPRDL_INC = 0;

        public DesignerForm()
        {
            KeyPreview = true;
            GetStartupState();
            //BuildMenus();
            InitializeComponent();
            Application.AddMessageFilter(this);

            this.MdiChildActivate += new EventHandler(RdlDesigner_MdiChildActivate);
            this.Closing += new System.ComponentModel.CancelEventHandler(this.RdlDesigner_Closing);
            _GetPassword = new NeedPassword(this.GetPassword);

            InitProperties();
            InitToolbar();
            InitStatusBar();
            InitIpc();

            // open up the current files if any
            if (_CurrentFiles != null)
            {
                foreach (string file in _CurrentFiles)
                {
                    CreateMDIChild(file, null, false);
                }
                _CurrentFiles = null;		// don't need this any longer
            }

            DesignTabChanged(this, new EventArgs());	
        }

        public DesignerForm(List<string> files)
        {
            KeyPreview = true;
            GetStartupState();
            //BuildMenus();
            InitializeComponent();
            Application.AddMessageFilter(this);

            this.MdiChildActivate += new EventHandler(RdlDesigner_MdiChildActivate);
            this.Closing += new System.ComponentModel.CancelEventHandler(this.RdlDesigner_Closing);
            _GetPassword = new NeedPassword(this.GetPassword);

            InitProperties();
            InitToolbar();
            InitStatusBar();
            InitIpc();

            _CurrentFiles = files;
            // open up the current files if any
            if (_CurrentFiles != null)
            {
                foreach (string file in _CurrentFiles)
                {
                    CreateMDIChild(file, null, false);
                }
                _CurrentFiles = null;		// don't need this any longer
            }

            DesignTabChanged(this, new EventArgs());
        }

        private void GetStartupState()
        {
            string optFileName = AppDomain.CurrentDomain.BaseDirectory + "designerstate.xml";
            _RecentFiles = new SortedList<DateTime, string>();
            _CurrentFiles = new List<string>();
            _HelpUrl = DefaultHelpUrl;				// set as default
            _SupportUrl = DefaultSupportUrl;

            try
            {
                XmlDocument xDoc = new XmlDocument();
                xDoc.PreserveWhitespace = false;
                xDoc.Load(optFileName);
                XmlNode xNode;
                xNode = xDoc.SelectSingleNode("//designerstate");

                string[] args = Environment.GetCommandLineArgs();
                for (int i = 1; i < args.Length; i++)
                {
                    string larg = args[i].ToLower();
                    if (larg == "/m" || larg == "-m")
                        continue;

                    if (File.Exists(args[i]))			// only add it if it exists
                        _CurrentFiles.Add(args[i]);
                }

                // Loop thru all the child nodes
                foreach (XmlNode xNodeLoop in xNode.ChildNodes)
                {
                    switch (xNodeLoop.Name)
                    {
                        case "RecentFiles":
                            DateTime now = DateTime.Now;
                            now = now.Subtract(new TimeSpan(0, 1, 0, 0, 0));	// subtract an hour
                            foreach (XmlNode xN in xNodeLoop.ChildNodes)
                            {
                                string file = xN.InnerText.Trim();
                                if (File.Exists(file))			// only add it if it exists
                                {
                                    _RecentFiles.Add(now, file);
                                    now = now.AddSeconds(1);
                                }
                            }
                            break;
                        case "RecentFilesMax":
                            try
                            {
                                this._RecentFilesMax = Convert.ToInt32(xNodeLoop.InnerText);
                            }
                            catch
                            {
                                this._RecentFilesMax = 5;
                            }
                            break;
                        case "CurrentFiles":
                            if (_CurrentFiles.Count > 0)	// don't open other current files if opened with argument
                                break;
                            foreach (XmlNode xN in xNodeLoop.ChildNodes)
                            {
                                string file = xN.InnerText.Trim();
                                if (File.Exists(file))			// only add it if it exists
                                    _CurrentFiles.Add(file);
                            }
                            break;
                        case "Toolbar":
                            _Toolbar = new List<string>();
                            foreach (XmlNode xN in xNodeLoop.ChildNodes)
                            {
                                string item = xN.InnerText.Trim();
                                _Toolbar.Add(item);
                            }
                            break;
                        case "Help":
                            if (xNodeLoop.InnerText.Length > 0)		//empty means to use the default
                                _HelpUrl = xNodeLoop.InnerText;
                            break;
                        case "Support":
                            if (xNodeLoop.InnerText.Length > 0)		//empty means to use the default
                                _SupportUrl = xNodeLoop.InnerText;
                            break;
                        case "EditLines":
                            _ShowEditLines = (xNodeLoop.InnerText.ToLower() == "true");
                            break;
                        case "ShowPreviewWaitDialog":
                            _ShowPreviewWaitDialog = (xNodeLoop.InnerText.ToLower() == "true");
                            break;
                        case "OutlineReportItems":
                            this.ShowReportItemOutline = (xNodeLoop.InnerText.ToLower() == "true");
                            break;
                        case "ShowTabbedInterface":
                            this._ShowTabbedInterface = (xNodeLoop.InnerText.ToLower() == "true");
                            break;
                        case "PropertiesLocation":
                            this._PropertiesLocation = GetPropertiesDockStyle(xNodeLoop.InnerText);
                            break;
                        case "PropertiesAutoHide":
                            this._PropertiesAutoHide = (xNodeLoop.InnerText.ToLower() == "true");
                            break;
                        case "MapSubtypes":
                            RdlDesigner.MapSubtypes = xNodeLoop.InnerText.Split(new char[] { ',' });
                            break;

                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {		// Didn't sucessfully get the startup state but don't really care
                Console.WriteLine(string.Format("Exception in GetStartupState ignored.\n{0}\n{1}", ex.Message, ex.StackTrace));
            }

            if (_Toolbar == null)		// Use this as the default toolbar
                _Toolbar = this.ToolbarDefault;
            return;
        }

        private void SaveStartupState()
        {
            try
            {
                int[] colors = GetCustomColors();		// get custom colors

                XmlDocument xDoc = new XmlDocument();
                XmlProcessingInstruction xPI;
                xPI = xDoc.CreateProcessingInstruction("xml", "version='1.0' encoding='UTF-8'");
                xDoc.AppendChild(xPI);

                XmlNode xDS = xDoc.CreateElement("designerstate");
                xDoc.AppendChild(xDS);

                XmlNode xN;
                // Loop thru the current files
                XmlNode xFiles = xDoc.CreateElement("CurrentFiles");
                xDS.AppendChild(xFiles);
                foreach (MDIChild mc in this.MdiChildren)
                {
                    string file = mc.SourceFile;
                    if (file == null)
                        continue;
                    xN = xDoc.CreateElement("file");
                    xN.InnerText = file;
                    xFiles.AppendChild(xN);
                }

                // Recent File Count
                XmlNode rfc = xDoc.CreateElement("RecentFilesMax");
                xDS.AppendChild(rfc);
                rfc.InnerText = this._RecentFilesMax.ToString();

                // Loop thru recent files list
                xFiles = xDoc.CreateElement("RecentFiles");
                xDS.AppendChild(xFiles);
                foreach (string f in _RecentFiles.Values)
                {
                    xN = xDoc.CreateElement("file");
                    xN.InnerText = f;
                    xFiles.AppendChild(xN);
                }

                // Help File URL
                XmlNode hfu = xDoc.CreateElement("Help");
                xDS.AppendChild(hfu);
                hfu.InnerText = this._HelpUrl;

                // Map chart subtypes
                XmlNode hmap = xDoc.CreateElement("MapSubtypes");
                xDS.AppendChild(hmap);
                StringBuilder maps = new StringBuilder();
                for (int mi = 0; mi < MapSubtypes.Length; mi++)
                {
                    maps.Append(MapSubtypes[mi]);
                    if (mi + 1 < MapSubtypes.Length)
                        maps.Append(',');
                }
                hmap.InnerText = maps.ToString();

                // Show Line numbers
                XmlNode bln = xDoc.CreateElement("EditLines");
                xDS.AppendChild(bln);
                bln.InnerText = this._ShowEditLines ? "true" : "false";

                // Show Preview Wait dialog
                XmlNode pwd = xDoc.CreateElement("ShowPreviewWaitDialog");
                xDS.AppendChild(pwd);
                pwd.InnerText = this.ShowPreviewWaitDialog ? "true" : "false";

                // Outline reportitems
                XmlNode ori = xDoc.CreateElement("OutlineReportItems");
                xDS.AppendChild(ori);
                ori.InnerText = this.ShowReportItemOutline ? "true" : "false";

                // ShowTabbedInterface
                XmlNode sti = xDoc.CreateElement("ShowTabbedInterface");
                xDS.AppendChild(sti);
                sti.InnerText = this._ShowTabbedInterface ? "true" : "false";

                // PropertiesAutoHide
                XmlNode pah = xDoc.CreateElement("PropertiesAutoHide");
                xDS.AppendChild(pah);
                pah.InnerText = this._PropertiesAutoHide ? "true" : "false";

                // PropertiesLocation
                string loc = "right";
                switch (_PropertiesLocation)
                {
                    case DockStyle.Left:
                        loc = "left";
                        break;
                    case DockStyle.Top:
                        loc = "top";
                        break;
                    case DockStyle.Bottom:
                        loc = "bottom";
                        break;
                }
                XmlNode pl = xDoc.CreateElement("PropertiesLocation");
                xDS.AppendChild(pl);
                pl.InnerText = loc;

                // Save the toolbar items
                XmlNode xTB = xDoc.CreateElement("Toolbar");
                xDS.AppendChild(xTB);
                foreach (string t in _Toolbar)
                {
                    xN = xDoc.CreateElement("item");
                    xN.InnerText = t;
                    xTB.AppendChild(xN);
                }

                // Save the custom colors
                StringBuilder sb = new StringBuilder();
                foreach (int c in colors)
                {
                    sb.Append(c.ToString());
                    sb.Append(",");
                }
                sb.Remove(sb.Length - 1, 1);	// remove last ","

                xN = xDoc.CreateElement("CustomColors");
                xN.InnerText = sb.ToString();
                xDS.AppendChild(xN);

                // Save the file
                string optFileName = AppDomain.CurrentDomain.BaseDirectory + "designerstate.xml";

                xDoc.Save(optFileName);
            }
            catch { }		// still want to leave even on error

            return;
        }

        /// <summary>
        /// Handles mousewheel processing when window under mousewheel doesn't have focus
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public bool PreFilterMessage(ref Message m)
        {
#if MONO
            return false;
#else
            if (m.Msg == 0x20a)
            {
                // WM_MOUSEWHEEL, find the control at screen position m.LParam
                Point pos = new Point(m.LParam.ToInt32() & 0xffff, m.LParam.ToInt32() >> 16);
                IntPtr hWnd = WindowFromPoint(pos);
                if (hWnd != IntPtr.Zero && hWnd != m.HWnd && Control.FromHandle(hWnd) != null)
                {
                    SendMessage(hWnd, m.Msg, m.WParam, m.LParam);
                    return true;
                }
            }
            return false;
#endif
        }
#if MONO
#else
        // P/Invoke declarations
        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(Point pt);
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
#endif
        /// <summary>
        /// InitIpc starts up the inter-process commmunciation capabilities.  For now it is only used to 
        /// enable only a single RdlDesigner .exe is up and running at any one time.
        /// </summary>
        private void InitIpc()
        {
            // considered using FileWatcher but it has restrictions on type of file system it will watch
            //    if want to do real Ipc should use TCP (see http://msdn2.microsoft.com/en-us/library/Aa446520.aspx )
            _IpcTimer = new Timer();
            _IpcTimer.Interval = 1000;       // every second
            _IpcTimer.Tick += new EventHandler(Ipc_Tick);
            _IpcTimer.Start();
        }
        /// <summary>
        /// Handle the timer tick event.   Check if the IPC file has been created.  If so then 
        /// handle each command (line in file).  If it isn't a command then it is assumed to be
        /// a file name that should be opened.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Ipc_Tick(object sender, EventArgs e)
        {
            lock (_IpcTimer)
            {
                string filename = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + DesignerForm.IpcFileName;
                if (!File.Exists(filename))
                    return;

                try
                {
                    using (StreamReader sr = new StreamReader(filename, Encoding.Unicode))
                    {
                        while (!sr.EndOfStream)
                        {
                            string cmd = sr.ReadLine();
                            //                          Console.WriteLine(cmd);
                            if (cmd.StartsWith("/a", StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (this.WindowState == FormWindowState.Minimized)
                                    this.WindowState = FormWindowState.Normal;
                                this.Activate();
                            }
                            else
                            {
                                CreateMDIChild(cmd, null, true);
                            }
                        }
                    }
                    File.Delete(filename);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception in Ipc_Tick:" + ex.Message);
                }
            }
        }

        private void InitProperties()
        {
            if (mainProperties != null)
            {
                this.Container.Remove(mainProperties);
                this.Container.Remove(mainSp);
                mainProperties = null;
                mainSp = null;
            }

            if (!_ShowProperties)
                return;

            mainSp = new Splitter();
            mainSp.Parent = this;
            mainSp.Dock = _PropertiesLocation;

            mainProperties = new PropertyCtl();
            mainProperties.Parent = this;

            // Left
            mainProperties.Dock = _PropertiesLocation;
            mainProperties.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        }

        private DockStyle GetPropertiesDockStyle(string l)
        {
            DockStyle ds;

            switch (l.Trim().ToLower())
            {
                case "left":
                    ds = DockStyle.Left;     // left
                    break;
                case "top":
                    ds = DockStyle.Top;     // top
                    break;
                case "bottom":
                    ds = DockStyle.Bottom;   // bottom
                    break;
                case "right":
                default:
                    ds = DockStyle.Right;    // right
                    break;
            }
            return ds;
        }

        private void InitStatusBar()
        {
            mainSB = new StatusBar();

            mainSB.Parent = this;
            mainSB.Dock = DockStyle.Bottom;
            mainSB.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            statusPrimary = new StatusBarPanel();
            statusPrimary.Width = 260;
            statusPrimary.MinWidth = 10;
            statusPrimary.AutoSize = StatusBarPanelAutoSize.Spring;
            statusPrimary.Text = "";
            statusPrimary.BorderStyle = StatusBarPanelBorderStyle.None;

            statusSelected = new StatusBarPanel();
            statusSelected.AutoSize = StatusBarPanelAutoSize.Contents;
            statusSelected.Alignment = HorizontalAlignment.Center;
            statusSelected.ToolTipText = "Name of selected ReportItem";
            statusSelected.Width = 192;
            statusSelected.MinWidth = 10;
            statusSelected.Text = "";
            statusSelected.BorderStyle = StatusBarPanelBorderStyle.Sunken;

            statusPosition = new StatusBarPanel();
            statusPosition.AutoSize = StatusBarPanelAutoSize.Contents;
            statusPosition.ToolTipText = "Position of selected ReportItem";
            statusPosition.Width = 96;
            statusPosition.MinWidth = 10;
            statusPosition.Alignment = HorizontalAlignment.Center;
            statusPosition.Text = "";
            statusPosition.BorderStyle = StatusBarPanelBorderStyle.Raised;

            mainSB.Size = new System.Drawing.Size(472, 22);

            mainSB.ShowPanels = true;
            mainSB.Location = new System.Drawing.Point(0, 282);
            mainSB.Name = "mainSB";
            mainSB.Panels.AddRange(new System.Windows.Forms.StatusBarPanel[] {
																			  this.statusPrimary,
																			  this.statusSelected,
																			  this.statusPosition});
            this.Controls.Add(mainSB);
        }

        private void InitToolbar()
        {
            bool bResumeLayout = false;
            // Clear out anything from before
            if (mainTB != null)
            {
                this.SuspendLayout();
                mainTB.SuspendLayout();
                bResumeLayout = true;
                mainTB.Controls.Clear();
                if (ctlLAlign != null) { ctlLAlign = null; }
                if (ctlRAlign != null) { ctlRAlign = null; }
                if (ctlCAlign != null) { ctlCAlign = null; }
                if (ctlBold != null) { ctlBold = null; }
                if (ctlItalic != null) { ctlItalic = null; }
                if (ctlUnderline != null) { ctlUnderline = null; }
                if (ctlFont != null) { ctlFont = null; }
                if (ctlFontSize != null) { ctlFontSize = null; }
                if (ctlForeColor != null) { ctlForeColor = null; }
                if (ctlBackColor != null) { ctlBackColor = null; }
                if (ctlNew != null) { ctlNew = null; }
                if (ctlOpen != null) { ctlOpen = null; }
                if (ctlSave != null) { ctlSave = null; }
                if (ctlCut != null) { ctlCut = null; }
                if (ctlCopy != null) { ctlCopy = null; }
                if (ctlUndo != null) { ctlUndo = null; }
                if (ctlPaste != null) { ctlPaste = null; }
                if (ctlPrint != null) { ctlPrint = null; }
                if (ctlPdf != null) { ctlPdf = null; }
                if (ctlTif != null) { ctlTif = null; }
                if (ctlXml != null) { ctlXml = null; }
                if (ctlHtml != null) { ctlHtml = null; }
                if (ctlMht != null) { ctlMht = null; }
                if (ctlCsv != null) { ctlCsv = null; }
                if (ctlExcel != null) { ctlExcel = null; }
                if (ctlRtf != null) { ctlRtf = null; }
                if (ctlZoom != null) { ctlZoom = null; }
                if (ctlInsertCurrent != null) { ctlInsertCurrent = null; }
                if (ctlInsertTextbox != null) { ctlInsertTextbox = null; }
                if (ctlInsertChart != null) { ctlInsertChart = null; }
                if (ctlInsertRectangle != null) { ctlInsertRectangle = null; }
                if (ctlInsertTable != null) { ctlInsertTable = null; }
                if (ctlInsertMatrix != null) { ctlInsertMatrix = null; }
                if (ctlInsertList != null) { ctlInsertList = null; }
                if (ctlInsertLine != null) { ctlInsertLine = null; }
                if (ctlInsertImage != null) { ctlInsertImage = null; }
                if (ctlInsertSubreport != null) { ctlInsertSubreport = null; }
                if (ctlSelectTool != null) { ctlSelectTool = null; }
                if (ctlEditTextbox != null) { ctlEditTextbox = null; }
                if (ctlEditLabel != null) { ctlEditLabel = null; }
            }
            else
            {
                mainTB = new ToolBar();
                mainTB.SizeChanged += new EventHandler(mainTB_SizeChanged);
                mainTB.SuspendLayout();
            }
            const int LINEHEIGHT = 22;
            const int LEFTMARGIN = 5;
            int y = 2;
            int x = LEFTMARGIN;

            System.Resources.ResourceManager resources = new System.Resources.ResourceManager(typeof(DesignerForm));

            // Build the controls the user wants
            foreach (string tbi in _Toolbar)
            {
                switch (tbi)
                {
                    case "\n":
                    case "Newline":
                        y += LINEHEIGHT;
                        x = LEFTMARGIN;
                        break;
                    case "Align":
                        ctlLAlign = InitToolbarInsertToggle(ref x, y, "Left Align", bLAlign.Image, new EventHandler(this.tsmFormatAlignButton_Click));
                        ctlCAlign = InitToolbarInsertToggle(ref x, y, "Center Align", bCAlign.Image, new EventHandler(this.tsmFormatAlignButton_Click));
                        ctlRAlign = InitToolbarInsertToggle(ref x, y, "Right Align", bRAlign.Image, new EventHandler(this.tsmFormatAlignButton_Click));
                        break;
                    case "Bold":
                        x += InitToolbarBold(x, y);
                        break;
                    case "Italic":
                        x += InitToolbarItalic(x, y);
                        break;
                    case "Underline":
                        x += InitToolbarUnderline(x, y);
                        break;
                    case "Space":
                        x += 5;
                        break;
                    case "Font":
                        x += InitToolbarFont(x, y);
                        break;
                    case "FontSize":
                        x += InitToolbarFontSize(x, y);
                        break;
                    case "ForeColor":
                        ctlForeColor = InitToolbarColor(ref x, y, "Fore Color", new PopupEventHandler(tip_Popup_Fore));
                        ctlForeColor.SelectedValueChanged += new EventHandler(ctlForeColor_Change);
                        ctlForeColor.Validated += new EventHandler(ctlForeColor_Change);
                        break;
                    case "BackColor":
                        ctlBackColor = InitToolbarColor(ref x, y, "Back Color", new PopupEventHandler(tip_Popup_Back));
                        ctlBackColor.SelectedValueChanged += new EventHandler(ctlBackColor_Change);
                        ctlBackColor.Validated += new EventHandler(ctlBackColor_Change);
                        break;
                    case "New":
                        ctlNew = InitToolbarMenu(ref x, y, "New", bNew.Image, new EventHandler(this.tsmFileNewReport_Click));
                        break;
                    case "Open":
                        ctlOpen = InitToolbarMenu(ref x, y, "Open", bOpen.Image, new EventHandler(this.tsmFileOpen_Click));
                        break;
                    case "Save":
                        ctlSave = InitToolbarMenu(ref x, y, "Save", bSave.Image, new EventHandler(this.tsmFileSave_Click));
                        break;
                    case "Cut":
                        ctlCut = InitToolbarMenu(ref x, y, "Cut", bCut.Image, new EventHandler(this.tsmEditCut_Click));
                        break;
                    case "Copy":
                        ctlCopy = InitToolbarMenu(ref x, y, "Copy", bCopy.Image, new EventHandler(this.tsmEditCopy_Click));
                        break;
                    case "Undo":
                        ctlUndo = InitToolbarMenu(ref x, y, "Undo", bUndo.Image, new EventHandler(this.tsmEditUndo_Click));
                        break;
                    case "Paste":
                        ctlPaste = InitToolbarMenu(ref x, y, "Paste", bPaste.Image, new EventHandler(this.tsmEditPaste_Click));
                        break;
                    case "Print":
                        ctlPrint = InitToolbarMenu(ref x, y, "Print", bPrint.Image, new EventHandler(this.tsmFilePrint_Click));
                        break;
                    case "SelectTool":
                        x += InitToolbarSelectTool(x, y);
                        break;
                    case "XML":
                        ctlXml = InitToolbarMenu(ref x, y, "XML", bXml.Image, new EventHandler(this.tsmExportXml_Click));
                        break;
                    //case "PDF":
                    //    ctlPdf = InitToolbarMenu(ref x, y, "PDF", bPdf.Image, new EventHandler(this.tsmExportPdf_Click));
                    //    break;
                    //case "TIF":
                    //    ctlTif = InitToolbarMenu(ref x, y, "TIF", bTif.Image, new EventHandler(this.tsmExportTif_Click));
                    //    break;
                    //case "HTML":
                    //    ctlHtml = InitToolbarMenu(ref x, y, "HTML", bHtml.Image, new EventHandler(this.tsmExportHtml_Click));
                    //    break;
                    //case "MHT":
                    //    ctlMht = InitToolbarMenu(ref x, y, "MHT", bMht.Image, new EventHandler(this.tsmExportMHtml_Click));
                    //    break;
                    //case "CSV":
                    //    ctlCsv = InitToolbarMenu(ref x, y, "CSV", bCsv.Image, new EventHandler(this.tsmExportCsv_Click));
                    //    break;
                    //case "Excel":
                    //    ctlExcel = InitToolbarMenu(ref x, y, "Excel", bExcel.Image, new EventHandler(this.tsmExportExcel_Click));
                    //    break;
                    //case "RTF":
                    //    ctlRtf = InitToolbarMenu(ref x, y, "RTF", bRtf.Image, new EventHandler(this.tsmExportRtf_Click));
                    //    break;
                    case "Edit":
                        ctlEditTextbox = InitToolbarEditTextbox(ref x, y);
                        break;
                    case "Textbox":
                        ctlInsertTextbox = InitToolbarInsertToggle(ref x, y, "Textbox", bText.Image);
                        break;
                    case "Chart":
                        ctlInsertChart = InitToolbarInsertToggle(ref x, y, "Chart", bChart.Image);
                        break;
                    case "System.Drawing.Rectangle":
                        ctlInsertRectangle = InitToolbarInsertToggle(ref x, y, "System.Drawing.Rectangle", bRectangle.Image);
                        break;
                    case "Table":
                        ctlInsertTable = InitToolbarInsertToggle(ref x, y, "Table", bTable.Image);
                        break;
                    case "Matrix":
                        ctlInsertMatrix = InitToolbarInsertToggle(ref x, y, "Matrix", bMatrix.Image);
                        break;
                    case "List":
                        ctlInsertList = InitToolbarInsertToggle(ref x, y, "List", bList.Image);
                        break;
                    case "Line":
                        ctlInsertLine = InitToolbarInsertToggle(ref x, y, "Line", bLine.Image);
                        break;
                    case "System.Drawing.Image":
                        ctlInsertImage = InitToolbarInsertToggle(ref x, y, "System.Drawing.Image", bImage.Image);
                        break;
                    case "Subreport":
                        ctlInsertSubreport = InitToolbarInsertToggle(ref x, y, "Subreport", bSubreport.Image);
                        break;
                    case "Zoom":
                        ctlZoom = InitToolbarZoom(ref x, y);
                        ctlZoom.SelectedValueChanged += new EventHandler(ctlZoom_Change);
                        ctlZoom.Validated += new EventHandler(ctlZoom_Change);
                        break;
                    default:
                        break;
                }
            }

            // put the tab control in
            if (mainTC == null)
            {
                mainTC = new Oranikle.Studio.Controls.CtrlStyledTab();
                mainTC.MouseClick += new MouseEventHandler(mainTC_MouseClick);

                mainTC.SelectedIndexChanged += new EventHandler(mainTC_SelectedIndexChanged);
                mainTC.ShowToolTips = true;
            }
            mainTC.Parent = mainTB;
            mainTC.Visible = _ShowTabbedInterface;
            if (_ShowTabbedInterface)
            {   // When tabbed we force the mdi children to be maximized (on reset)
                foreach (MDIChild mc in this.MdiChildren)
                {
                    mc.WindowState = FormWindowState.Maximized;
                }
            }
            mainTC.Location = new Point(0, y + LINEHEIGHT + 1);
            mainTC.Size = new Size(mainTB.Width, LINEHEIGHT);
            if (_ShowTabbedInterface)
                y += LINEHEIGHT;

            mainTB.Parent = this;
            mainTB.BorderStyle = BorderStyle.None;
            mainTB.DropDownArrows = true;
            mainTB.Name = "mainTB";
            mainTB.ShowToolTips = true;
            //			mainTB.Size = new Size(440,20);
            mainTB.TabIndex = 1;
            mainTB.AutoSize = false;
            mainTB.Height = y + LINEHEIGHT + 1;     // 1 was 4	
            if (bResumeLayout)
            {
                mainTB.ResumeLayout();
                this.ResumeLayout();
            }
        }
        /// <summary>
        /// Handles right mouse button processing on current tab
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void mainTC_MouseClick(object sender, MouseEventArgs e)
        {
            TabControl tc = sender as TabControl;
            if (tc == null)
                return;

            if (e.Button != MouseButtons.Right)
                return;
            Point p = e.Location;
            int current = -1;
            for (int i = 0; i < tc.TabCount; i++)
            {
                System.Drawing.Rectangle r = tc.GetTabRect(i);
                if (r.Contains(p))
                {
                    current = i;
                    break;
                }
            }
            if (current != tc.SelectedIndex)
                return;             // didn't find the tab

            //ToolStripMenuItem mc = new ToolStripMenuItem("&Close", new EventHandler(this.tsmFileClose_Click));
            //ToolStripMenuItem ms = new ToolStripMenuItem("&Save", new EventHandler(this.tsmFileSave_Click));
            //ToolStripMenuItem ma = new ToolStripMenuItem("Close All But This", new EventHandler(tsmWndCloseAllButCurrent_Click));
            //ContextMenu cm = new ContextMenu();
            //cm.MenuItems.AddRange(new MenuItem[] { ms, mc, ma });
            //cm.Show(tc, p);

        }

        void mainTB_SizeChanged(object sender, EventArgs e)
        {
            mainTC.Width = mainTB.Width;
        }

        void mainTC_SelectedIndexChanged(object sender, EventArgs e)
        {
            MDIChild mc = mainTC.SelectedTab == null ? null : mainTC.SelectedTab.Tag as MDIChild;
            mdi_Activate(mc);
        }
#if MONO
#else
        [DllImport("user32.dll")]
        public static extern bool LockWindowUpdate(IntPtr hWndLock);
#endif

        void mdi_Activate(MDIChild mc)
        {
            if (mc == null)
                return;
            if (bMono)
            {
                mc.Activate();
                this.Refresh(); //Forces a synchronous redraw of all controls
            }
            else
            {
#if MONO
                mc.Activate();
                this.Refresh(); //Forces a synchronous redraw of all controls
#else
                LockWindowUpdate(this.Handle);
                mc.Activate();
                this.Refresh(); //Forces a synchronous redraw of all controls

                LockWindowUpdate(IntPtr.Zero);
#endif
            }
        }


        internal int RecentFilesMax
        {
            get { return _RecentFilesMax; }
            set { _RecentFilesMax = value; }
        }

        internal NeedPassword SharedDatasetPassword
        {
            get { return _GetPassword; }
        }

        internal SortedList<DateTime, string> RecentFiles
        {
            get { return _RecentFiles; }
        }

        internal string HelpUrl
        {
            get
            {
                if (_HelpUrl != null && _HelpUrl.Length > 0)
                    return _HelpUrl;
                return DefaultHelpUrl;
            }
            set
            {
                _HelpUrl = value.Length > 0 ? value : DefaultHelpUrl;
            }
        }
        static internal string[] MapSubtypes
        {
            get
            {
                return _MapSubtypes;
            }
            set
            {
                _MapSubtypes = value;
            }
        }

        internal bool ShowPreviewWaitDialog
        {
            get { return _ShowPreviewWaitDialog; }
            set { _ShowPreviewWaitDialog = value; }
        }

        internal bool ShowEditLines
        {
            get
            {
                return _ShowEditLines;
            }
            set
            {
                _ShowEditLines = value;
            }
        }

        internal bool PropertiesAutoHide
        {
            get { return _PropertiesAutoHide; }
            set { _PropertiesAutoHide = value; }
        }

        internal DockStyle PropertiesLocation
        {
            get
            {
                return _PropertiesLocation;
            }
            set
            {
                if (_PropertiesLocation == value)
                    return;         // didn't change nothing to do
                _PropertiesLocation = value;

                mainSp.Dock = _PropertiesLocation;
                mainProperties.Dock = _PropertiesLocation;
                // Adjust the size of the property window so that it doesn't
                //   fill the whole main window
                switch (_PropertiesLocation)
                {
                    case DockStyle.Left:
                    case DockStyle.Right:
                        mainProperties.Width = this.Width / 3;
                        break;
                    case DockStyle.Top:
                    case DockStyle.Bottom:
                        mainProperties.Height = this.Height / 3;
                        break;
                }
            }
        }


        internal bool ShowReportItemOutline
        {
            get
            {
                return _ShowReportItemOutline;
            }
            set
            {
                _ShowReportItemOutline = value;
            }
        }

        internal bool ShowTabbedInterface
        {
            get { return _ShowTabbedInterface; }
            set { _ShowTabbedInterface = value; }
        }

        internal string SupportUrl
        {
            get
            {
                if (_SupportUrl != null && _SupportUrl.Length > 0)
                    return _SupportUrl;
                return DefaultSupportUrl;
            }
            set
            {
                _SupportUrl = value.Length > 0 ? value : DefaultSupportUrl;
            }
        }


        internal List<string> Toolbar
        {
            get { return _Toolbar; }
            set
            {
                _Toolbar = value;
                InitToolbar();			// reset the toolbar
            }
        }

        internal List<string> ToolbarDefault
        {
            get
            {
                return new List<string>(new string[] {
					"New", "Open", "Save", "Space", "Cut", "Copy", "Paste", "Undo", "Space", 
					"Textbox", "Chart", "Table", "List", "System.Drawing.Image", "Matrix", "Subreport", 
					"System.Drawing.Rectangle", "Line", "Space","Edit", "Newline",
					"Bold", "Italic", "Underline", "Space","Align","Space",
					"Font", "FontSize", "Space", "ForeColor", "BackColor", 
					"Space", "Print", "Space", "Zoom", "SelectTool", "Space", 
                    "PDF", "HTML", "Excel", "XML", "MHT", "CSV", "RTF", "TIF"});
                ;
            }
        }

        internal List<string> ToolbarAllowDups
        {
            get
            {
                return new List<string>(new string[] {
				  "Space",
				  "Newline"});
                ;
            }
        }

        /// <summary>
        /// All of the possible items that can be placed on a toolbar
        /// </summary>
        internal string[] ToolbarOperations
        {
            get
            {
                return new string[] 
					{"Newline",
                    "Align",
					"Bold",
					"Italic",
					"Underline",
					"Space",
					"Font",
					"FontSize",
					"ForeColor",
					"BackColor",
					"New",
					"Open",
					"Save",
					"Cut",
					"Copy",
					"Undo",
					"Paste",
					"Print",
					"XML",
					"PDF",
					"HTML",
                    "MHT",    
                    "CSV",
                    "RTF",
                    "Excel",
                    "TIF",
    				"Edit",
					"Textbox",
					"Chart",
					"System.Drawing.Rectangle",
					"Table",
					"Matrix",
					"List",
					"Line",
					"System.Drawing.Image",
                    "SelectTool",
					"Subreport",
					"Zoom"   };
            }
        }

        private Button InitToolbarMenu(ref int x, int y, string t, System.EventHandler handler)
        {
            return InitToolbarMenu(ref x, y, t, null, handler);
        }

        private Button InitToolbarMenu(ref int x, int y, string t, System.Drawing.Image img, System.EventHandler handler)
        {
            SimpleButton ctl = new SimpleButton(this);

            ctl.Click += handler;
            if (img == null)
            {
                ctl.Text = t;
                ctl.Font = new Font("Arial", 8, FontStyle.Regular);

                using (Graphics g = ctl.CreateGraphics())
                {
                    SizeF fs = g.MeasureString(ctl.Text, ctl.Font);
                    ctl.Height = (int)fs.Height + 8;		// 8 is for margin so entire text shows in button
                    ctl.Width = (int)fs.Width + 4;
                    ctl.TextAlign = ContentAlignment.MiddleCenter;
                }
            }
            else
            {
                ctl.Image = img;
                ctl.ImageAlign = ContentAlignment.MiddleCenter;
                ctl.Height = img.Height + 5;
                ctl.Width = img.Width + 8;
                ctl.Text = "";
            }
            ctl.Tag = t;
            ctl.Left = x;
            ctl.Top = y;
            ctl.FlatStyle = FlatStyle.Flat;
            ToolTip tip = new ToolTip();
            tip.AutomaticDelay = 500;
            tip.ShowAlways = true;
            tip.SetToolTip(ctl, t);
            mainTB.Controls.Add(ctl);

            x = x + ctl.Width;

            return ctl;
        }

        private Oranikle.Studio.Controls.CustomTextControl InitToolbarEditTextbox(ref int x, int y)
        {
            Oranikle.Studio.Controls.CustomTextControl tb = new Oranikle.Studio.Controls.CustomTextControl();
            Label l = this.ctlEditLabel = new Label();
            l.AutoSize = true;
            l.Font = new Font("Arial", 8, FontStyle.Bold | FontStyle.Italic);
            l.Text = "fx";
            l.Left = x;
            l.Top = y + (tb.Height / 2) - (l.Height / 2);

            l.MouseEnter += new EventHandler(fxExpr_MouseEnter);
            l.MouseLeave += new EventHandler(fxExpr_MouseLeave);
            l.Click += new EventHandler(fxExpr_Click);
            mainTB.Controls.Add(l);
            x += l.Width;
            tb.Left = x;
            tb.Width = 230;
            x += tb.Width;
            tb.Top = y;
            tb.Validated += new EventHandler(this.EditTextbox_Validated);	// handler for edit changes
            tb.KeyDown += new KeyEventHandler(EditTextBox_KeyDown);

            mainTB.Controls.Add(tb);
            return tb;
        }

        void fxExpr_MouseLeave(object sender, EventArgs e)
        {
            Label lbl = sender as Label;
            if (lbl == null)
                return;

            lbl.BackColor = _SaveExprBackColor;
        }

        void fxExpr_MouseEnter(object sender, EventArgs e)
        {
            Label lbl = sender as Label;
            if (lbl == null)
                return;
            _SaveExprBackColor = lbl.BackColor;
            lbl.BackColor = Color.LightGray;

            return;
        }

        void fxExpr_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null ||
                mc.DesignTab != "design" || mc.DrawCtl.SelectedCount != 1 ||
                mc.Editor == null)
                return;

            XmlNode tn = mc.DrawCtl.SelectedList[0];

            using (DialogExprEditor de = new DialogExprEditor(mc.DrawCtl, ctlEditTextbox.Text, tn))
            {
                // Display the UI editor dialog
                if (de.ShowDialog(this) == DialogResult.OK)
                {
                    ctlEditTextbox.Text = de.Expression;
                    mc.Editor.SetSelectedText(de.Expression);
                    SetProperties(mc);
                }
            }
        }

        private SimpleToggle InitToolbarInsertToggle(ref int x, int y, string t)
        {
            return InitToolbarInsertToggle(ref x, y, t, null, new EventHandler(this.Insert_Click));
        }

        private SimpleToggle InitToolbarInsertToggle(ref int x, int y, string t,
            System.Drawing.Image bImg)
        {
            return InitToolbarInsertToggle(ref x, y, t, bImg, new EventHandler(this.Insert_Click));
        }

        private SimpleToggle InitToolbarInsertToggle(ref int x, int y, string t,
            System.Drawing.Image bImg, EventHandler eh)
        {
            SimpleToggle ctl = new SimpleToggle();
            ctl.UpColor = this.BackColor;
            ctl.Click += eh;	// click handler for all inserts

            if (bImg == null)
            {
                ctl.Text = t;
                using (Graphics g = ctl.CreateGraphics())
                {
                    SizeF fs = g.MeasureString(ctl.Text, ctl.Font);
                    ctl.Height = (int)fs.Height + 8;	// 8 is for margins
                    ctl.Width = (int)fs.Width + 12;
                }
            }
            else
            {
                ctl.Image = bImg;
                ctl.ImageAlign = ContentAlignment.MiddleCenter;
                ctl.Height = bImg.Height + 5;
                ctl.Width = bImg.Width + 8;
                ctl.Text = "";
            }

            ctl.Tag = t;
            ctl.Left = x;
            ctl.Top = y;
            ctl.FlatStyle = FlatStyle.Flat;
            ToolTip tipb = new ToolTip();
            tipb.AutomaticDelay = 500;
            tipb.ShowAlways = true;
            tipb.SetToolTip(ctl, t);
            mainTB.Controls.Add(ctl);

            x = x + ctl.Width;

            return ctl;
        }

        private int InitToolbarSelectTool(int x, int y)
        {
            ctlSelectTool = new SimpleToggle();
            ctlSelectTool.Image = bSelectTool.Image;
            ctlSelectTool.ImageAlign = ContentAlignment.MiddleCenter;
            ctlSelectTool.Height = ctlSelectTool.Image.Height + 5;
            ctlSelectTool.Width = ctlSelectTool.Image.Width + 8;
            ctlSelectTool.Text = "";

            ctlSelectTool.Click += new EventHandler(ctlSelectTool_Click);

            ctlSelectTool.Tag = "Selection Tool";
            ctlSelectTool.Left = x;
            ctlSelectTool.Top = y;
            ctlSelectTool.FlatStyle = FlatStyle.Flat;
            ctlSelectTool.TextAlign = ContentAlignment.MiddleCenter;

            ToolTip tipb = new ToolTip();
            tipb.AutomaticDelay = 500;
            tipb.ShowAlways = true;
            tipb.SetToolTip(ctlSelectTool, "Selection Tool");
            mainTB.Controls.Add(ctlSelectTool);

            return ctlSelectTool.Width;
        }


        private int InitToolbarBold(int x, int y)
        {
            ctlBold = new SimpleToggle();
            ctlBold.UpColor = this.BackColor;

            ctlBold.Click += new EventHandler(ctlBold_Click);

            ctlBold.Font = new Font("Courier New", 10, FontStyle.Bold);
            ctlBold.Text = "B";
            using (Graphics g = ctlBold.CreateGraphics())
            {
                SizeF fs = g.MeasureString(ctlBold.Text, ctlBold.Font);
                ctlBold.Height = (int)fs.Height + 6;	// 6 is for margins
                ctlBold.Width = ctlBold.Height;
            }

            ctlBold.Tag = "bold";
            ctlBold.Left = x;
            ctlBold.Top = y;
            ctlBold.FlatStyle = FlatStyle.Flat;
            ctlBold.TextAlign = ContentAlignment.MiddleCenter;

            ToolTip tipb = new ToolTip();
            tipb.AutomaticDelay = 500;
            tipb.ShowAlways = true;
            tipb.SetToolTip(ctlBold, "Bold");
            mainTB.Controls.Add(ctlBold);

            return ctlBold.Width;
        }

        private int InitToolbarItalic(int x, int y)
        {
            ctlItalic = new SimpleToggle();
            ctlItalic.UpColor = this.BackColor;
            ctlItalic.Click += new EventHandler(ctlItalic_Click);

            ctlItalic.Font = new Font("Courier New", 10, FontStyle.Italic | FontStyle.Bold);
            ctlItalic.Text = "I";
            using (Graphics g = ctlItalic.CreateGraphics())
            {
                SizeF fs = g.MeasureString(ctlItalic.Text, ctlItalic.Font);
                ctlItalic.Height = (int)fs.Height + 6;	// 6 is for margins
                ctlItalic.Width = ctlItalic.Height;
            }

            ctlItalic.Tag = "italic";
            ctlItalic.Left = x;
            ctlItalic.Top = y;
            ctlItalic.FlatStyle = FlatStyle.Flat;
            ToolTip tipb = new ToolTip();
            tipb.AutomaticDelay = 500;
            tipb.ShowAlways = true;
            tipb.SetToolTip(ctlItalic, "Italic");
            mainTB.Controls.Add(ctlItalic);

            return ctlItalic.Width;
        }

        private int InitToolbarUnderline(int x, int y)
        {
            ctlUnderline = new SimpleToggle();
            ctlUnderline.UpColor = this.BackColor;
            ctlUnderline.Click += new EventHandler(ctlUnderline_Click);
            ctlUnderline.Font = new Font("Courier New", 10, FontStyle.Underline | FontStyle.Bold);
            ctlUnderline.Text = "U";
            using (Graphics g = ctlUnderline.CreateGraphics())
            {
                SizeF fs = g.MeasureString(ctlUnderline.Text, ctlUnderline.Font);
                ctlUnderline.Height = (int)fs.Height + 6;	// 6 is for margins
                ctlUnderline.Width = ctlUnderline.Height;
            }

            ctlUnderline.Tag = "italic";
            ctlUnderline.Left = x;
            ctlUnderline.Top = y;
            ctlUnderline.FlatStyle = FlatStyle.Flat;
            ToolTip tipb = new ToolTip();
            tipb.AutomaticDelay = 500;
            tipb.ShowAlways = true;
            tipb.SetToolTip(ctlUnderline, "Underline");
            mainTB.Controls.Add(ctlUnderline);

            return ctlUnderline.Width;
        }

        private int InitToolbarFont(int x, int y)
        {
            // Create the font
            ctlFont = new ComboBox();
            ctlFont.SelectedValueChanged += new EventHandler(ctlFont_Change);
            ctlFont.Validated += new EventHandler(ctlFont_Change);
            ctlFont.Left = x;
            ctlFont.Top = y;
            ctlFont.DropDownStyle = ComboBoxStyle.DropDown;

            foreach (FontFamily ff in FontFamily.Families)
            {
                ctlFont.Items.Add(ff.Name);
            }
            ToolTip tip = new ToolTip();
            tip.AutomaticDelay = 500;
            tip.ShowAlways = true;
            tip.SetToolTip(ctlFont, "Font");
            mainTB.Controls.Add(ctlFont);

            return ctlFont.Width;
        }

        private int InitToolbarFontSize(int x, int y)
        {
            // Create the font
            ctlFontSize = new ComboBox();
            ctlFontSize.SelectedValueChanged += new EventHandler(ctlFontSize_Change);
            ctlFontSize.Validated += new EventHandler(ctlFontSize_Change);
            ctlFontSize.Width = 42;
            ctlFontSize.Left = x;
            ctlFontSize.Top = y;
            ctlFontSize.DropDownStyle = ComboBoxStyle.DropDown;

            string[] sizes = new string[] { "8", "9", "10", "11", "12", "14", "16", "18", "20", "22", "24", "26", "28", "36", "48", "72" };
            ctlFontSize.Items.AddRange(sizes);
            ToolTip tip = new ToolTip();
            tip.AutomaticDelay = 500;
            tip.ShowAlways = true;
            tip.SetToolTip(ctlFontSize, "Font Size");
            mainTB.Controls.Add(ctlFontSize);

            return ctlFontSize.Width;
        }

        private ColorPicker InitToolbarColor(ref int x, int y, string t, PopupEventHandler peh)
        {
            // Create the font
            ColorPicker ctl = new ColorPicker();
            ctl.Width = 37;
            ctl.Left = x;
            ctl.Top = y;
            ctl.Tag = t;

            ToolTip tip = new ToolTip();
            tip.AutomaticDelay = 500;
            tip.ShowAlways = true;
            tip.SetToolTip(ctl, t);
            tip.Popup += peh;
            mainTB.Controls.Add(ctl);

            x += ctl.Width;
            return ctl;
        }

        void tip_Popup_Fore(object sender, PopupEventArgs e)
        {
            ToolTip tt = sender as ToolTip;
            if (tt == null)
                return;
            string title = null;
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc != null && mc.DesignTab == "design" && mc.DrawCtl.SelectedCount == 1)
            {
                title = ctlForeColor.Text;
            }

            tt.ToolTipTitle = title;
        }

        void tip_Popup_Back(object sender, PopupEventArgs e)
        {
            ToolTip tt = sender as ToolTip;
            if (tt == null)
                return;

            string title = null;
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc != null && mc.DesignTab == "design" && mc.DrawCtl.SelectedCount == 1)
            {
                title = ctlBackColor.Text;
            }

            tt.ToolTipTitle = title;
        }

        private ComboBox InitToolbarZoom(ref int x, int y)
        {
            ComboBox ctl = new ComboBox();
            ctl.Width = 85;
            ctl.Left = x;
            ctl.Top = y;
            ctl.Tag = "Zoom";
            ctl.DropDownStyle = ComboBoxStyle.DropDownList;

            ctl.Items.AddRange(StaticLists.ZoomList);
            ToolTip tip = new ToolTip();
            tip.AutomaticDelay = 500;
            tip.ShowAlways = true;
            tip.SetToolTip(ctl, "Zoom");
            mainTB.Controls.Add(ctl);

            x += ctl.Width;
            return ctl;
        }


        private bool OkToSave()
        {
            foreach (MDIChild mc in this.MdiChildren)
            {
                if (!mc.OkToClose())
                    return false;
            }
            return true;
        }

        private void tsmFile_Popup(object sender, EventArgs e)
        {
            // These tsms require an MDIChild in order to work
            bool bEnable = this.MdiChildren.Length > 0 ? true : false;
            //tsmClose.Enabled = bEnable;
            tsmSave.Enabled = bEnable;
            tsmSaveAs.Enabled = bEnable;

            MDIChild mc = this.ActiveMdiChild as MDIChild;
            //tsmPrint.Enabled = tsmExport.Enabled = (mc != null && mc.DesignTab == "preview");

            // Recent File is enabled when there exists some files 
            tsmRecentFiles.Enabled = this._RecentFiles.Count <= 0 ? false : true;
        }

        private void tsmFileClose_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc != null)
                mc.Close();
        }

        private void tsmFileExit_Click(object sender, EventArgs e)
        {
            if (!OkToSave())
                return;
            SaveStartupState();
            tsmToolsCloseProcess(false);
            CleanupTempFiles();
            Application.Exit();
            //			Environment.Exit(0);
        }

        private void tsmFileOpen_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            OpenFileDialog ofd = new OpenFileDialog();
            if (mc != null)
            {
                try
                {
                    ofd.InitialDirectory = Path.GetDirectoryName(mc.SourceFile);
                }
                catch
                {
                }
            }
            ofd.DefaultExt = "rdlc";
            ofd.Filter = "Report files (*rdlc)|*.rdlc|" +
                "All files (*.*)|*.*";
            ofd.FilterIndex = 1;
            ofd.CheckFileExists = true;
            ofd.Multiselect = true;
            try
            {
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    foreach (string file in ofd.FileNames)
                    {
                        CreateMDIChild(file, null, false);
                    }
                    RecentFilesMenu();		// update the tsm for recent files
                }
            }
            finally
            {
                ofd.Dispose();
            }
        }

        // Create an MDI child.   Only creates it if not already open.
        private MDIChild CreateMDIChild(string file, string rdl, bool bMenuUpdate)
        {
            MDIChild mcOpen = null;
            if (file != null)
            {
                file = file.Trim();

                foreach (MDIChild mc in this.MdiChildren)
                {
                    if (mc.SourceFile != null && file == mc.SourceFile.Trim())
                    {							// we found it
                        mcOpen = mc;
                        break;
                    }
                }
            }
            if (mcOpen == null)
            {
                MDIChild mc = null;
                try
                {
                    mc = new MDIChild(this.ClientRectangle.Width * 3 / 5, this.ClientRectangle.Height * 3 / 5);
                    mc.OnSelectionChanged += new MDIChild.RdlChangeHandler(SelectionChanged);
                    mc.OnSelectionMoved += new MDIChild.RdlChangeHandler(SelectionMoved);
                    mc.OnReportItemInserted += new MDIChild.RdlChangeHandler(ReportItemInserted);
                    mc.OnDesignTabChanged += new MDIChild.RdlChangeHandler(DesignTabChanged);
                    mc.OnOpenSubreport += new DesignCtl.OpenSubreportEventHandler(OpenSubReportEvent);
                    mc.OnHeightChanged += new DesignCtl.HeightEventHandler(HeightChanged);

                    mc.MdiParent = this;
                    if (this._ShowTabbedInterface)
                        mc.WindowState = FormWindowState.Maximized;
                    mc.Viewer.GetDataSourceReferencePassword = _GetPassword;
                    if (file != null)
                    {
                        mc.Viewer.Folder = Path.GetDirectoryName(file);
                        mc.SourceFile = file;
                        mc.Text = Path.GetFileName(file);
                        mc.Viewer.Folder = Path.GetDirectoryName(file);
                        mc.Viewer.ReportName = Path.GetFileNameWithoutExtension(file);
                        NoteRecentFiles(file, bMenuUpdate);
                    }
                    else
                    {
                        mc.SourceRdl = rdl;
                        mc.Viewer.ReportName = mc.Text = "Untitled";
                    }
                    mc.ShowEditLines(this._ShowEditLines);
                    mc.ShowReportItemOutline = this.ShowReportItemOutline;
                    mc.ShowPreviewWaitDialog(this._ShowPreviewWaitDialog);
                    // add to toolbar tab
                    TabPage tp = new TabPage();
                    tp.Location = new System.Drawing.Point(0, 0);
                    tp.Name = mc.Text;
                    tp.TabIndex = 1;
                    tp.Text = mc.Text;
                    tp.Tag = mc;                // tie the mdichild to the tabpage
                    tp.ToolTipText = file;
                    mainTC.Controls.Add(tp);
                    mc.Tab = tp;

                    mc.Show();
                    mcOpen = mc;

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    if (mc != null)
                        mc.Close();
                    return null;
                }
            }
            else
            {
                mcOpen.Activate();
            }
            return mcOpen;
        }

        private void DesignTabChanged(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            string tab = "";
            if (mc != null)
                tab = mc.DesignTab;
            bool bEnableEdit = false;
            bool bEnableDesign = false;
            bool bEnablePreview = false;
            bool bShowProp = _ShowProperties;
            switch (tab)
            {
                case "edit":
                    bEnableEdit = true;
                    if (_PropertiesAutoHide)
                        bShowProp = false;
                    break;
                case "design":
                    bEnableDesign = true;
                    break;
                case "preview":
                    if (_PropertiesAutoHide)
                        bShowProp = false;
                    bEnablePreview = true;
                    break;
            }
            if (!bEnableEdit && this._ValidateRdl != null)
            {
                this._ValidateRdl.Close();
            }
            mainProperties.Visible = mainSp.Visible = bShowProp;
            if (ctlLAlign != null)
                ctlLAlign.Enabled = bEnableDesign;
            if (ctlCAlign != null)
                ctlCAlign.Enabled = bEnableDesign;
            if (ctlRAlign != null)
                ctlRAlign.Enabled = bEnableDesign;
            if (ctlBold != null)
                ctlBold.Enabled = bEnableDesign;
            if (ctlItalic != null)
                ctlItalic.Enabled = bEnableDesign;
            if (ctlUnderline != null)
                ctlUnderline.Enabled = bEnableDesign;
            if (ctlFont != null)
                ctlFont.Enabled = bEnableDesign;
            if (ctlFontSize != null)
                ctlFontSize.Enabled = bEnableDesign;
            if (ctlForeColor != null)
                ctlForeColor.Enabled = bEnableDesign;
            if (ctlBackColor != null)
                ctlBackColor.Enabled = bEnableDesign;
            if (ctlCut != null)
                ctlCut.Enabled = bEnableDesign | bEnableEdit;
            if (ctlCopy != null)
                ctlCopy.Enabled = bEnableDesign | bEnableEdit | bEnablePreview;
            if (ctlUndo != null)
                ctlUndo.Enabled = bEnableDesign | bEnableEdit;
            if (ctlPaste != null)
                ctlPaste.Enabled = bEnableDesign | bEnableEdit;
            if (ctlPrint != null)
                ctlPrint.Enabled = bEnablePreview;

            if (ctlInsertTextbox != null)
                ctlInsertTextbox.Enabled = bEnableDesign;
            if (ctlSelectTool != null)
            {
                ctlSelectTool.Enabled = bEnablePreview;
                ctlSelectTool.Checked = mc == null ? false : mc.SelectionTool;
            }
            if (ctlInsertChart != null)
                ctlInsertChart.Enabled = bEnableDesign;
            if (ctlInsertRectangle != null)
                ctlInsertRectangle.Enabled = bEnableDesign;
            if (ctlInsertTable != null)
                ctlInsertTable.Enabled = bEnableDesign;
            if (ctlInsertMatrix != null)
                ctlInsertMatrix.Enabled = bEnableDesign;
            if (ctlInsertList != null)
                ctlInsertList.Enabled = bEnableDesign;
            if (ctlInsertLine != null)
                ctlInsertLine.Enabled = bEnableDesign;
            if (ctlInsertImage != null)
                ctlInsertImage.Enabled = bEnableDesign;
            if (ctlInsertSubreport != null)
                ctlInsertSubreport.Enabled = bEnableDesign;
            if (ctlPdf != null)
                ctlPdf.Enabled = bEnablePreview;
            if (ctlTif != null)
                ctlTif.Enabled = bEnablePreview;
            if (ctlXml != null)
                ctlXml.Enabled = bEnablePreview;
            if (ctlHtml != null)
                ctlHtml.Enabled = bEnablePreview;
            if (ctlMht != null)
                ctlMht.Enabled = bEnablePreview;
            if (ctlCsv != null)
                ctlCsv.Enabled = bEnablePreview;
            if (ctlExcel != null)
                ctlExcel.Enabled = bEnablePreview;
            if (ctlRtf != null)
                ctlRtf.Enabled = bEnablePreview;

            this.EnableEditTextBox();

            if (ctlZoom != null)
            {
                ctlZoom.Enabled = bEnablePreview;
                string zText = "Actual Size";
                if (mc != null)
                {
                    switch (mc.ZoomMode)
                    {
                        case ZoomEnum.FitWidth:
                            zText = "Fit Width";
                            break;
                        case ZoomEnum.FitPage:
                            zText = "Fit Page";
                            break;
                        case ZoomEnum.UseZoom:
                            if (mc.Zoom == 1)
                                zText = "Actual Size";
                            else
                                zText = string.Format("{0:0}", mc.Zoom * 100f);
                            break;
                    }
                    ctlZoom.Text = zText;
                }
            }
            // when no active sheet
            if (this.ctlSave != null)
                this.ctlSave.Enabled = mc != null;

            // Update the status and position information
            SetStatusNameAndPosition();
        }

        private void EnableEditTextBox()
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            bool bEnable = false;

            if (this.ctlEditTextbox == null || mc == null ||
                mc.DesignTab != "design" || mc.DrawCtl.SelectedCount != 1)
            { }
            else
            {
                XmlNode tn = mc.DrawCtl.SelectedList[0] as XmlNode;
                if (tn != null && tn.Name == "Textbox")
                {
                    ctlEditTextbox.Text = mc.DrawCtl.GetElementValue(tn, "Value", "");
                    bEnable = true;
                }
            }
            if (ctlEditTextbox != null)
            {
                if (!bEnable)
                    ctlEditTextbox.Text = "";
                ctlEditTextbox.Enabled = bEnable;
                ctlEditLabel.Enabled = bEnable;
            }
        }

        private void ReportItemInserted(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            // turn off the current selection after an item is inserted
            if (ctlInsertCurrent != null)
            {
                ctlInsertCurrent.Checked = false;
                mc.CurrentInsert = null;
                ctlInsertCurrent = null;
            }
        }

        private void OpenSubReportEvent(object sender, SubReportEventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            string file = mc.Viewer.Folder;
            if (e.SubReportName[0] == Path.DirectorySeparatorChar)
                file = file + e.SubReportName;
            else
                file = file + Path.DirectorySeparatorChar + e.SubReportName + ".rdlc";

            CreateMDIChild(file, null, true);
        }

        private void HeightChanged(object sender, HeightEventArgs e)
        {
            if (e.Height == null)
            {
                SetProperties(this.ActiveMdiChild as MDIChild);

                statusPosition.Text = "";
                return;
            }

            RegionInfo rinfo = new RegionInfo(CultureInfo.CurrentCulture.LCID);
            float h = DesignXmlDraw.GetSize(e.Height);
            string sh;
            if (rinfo.IsMetric)
            {
                sh = string.Format("   height={0:0.00}cm        ",
                        h / (DesignXmlDraw.POINTSIZED / 2.54d));
            }
            else
            {
                sh = string.Format("   height={0:0.00}\"        ",
                        h / DesignXmlDraw.POINTSIZED);
            }
            statusPosition.Text = sh;
        }

        private void SelectionMoved(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            SetStatusNameAndPosition();
        }

        private void SelectionChanged(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;
            // handle edit tab first
            if (mc.RdlEditor.DesignTab == "edit")
            {
                SetStatusNameAndPosition();
                return;
            }

            bSuppressChange = true;			// don't process changes in status bar

            SetStatusNameAndPosition();
            this.EnableEditTextBox();	// handling enabling/disabling of textbox

            StyleInfo si = mc.SelectedStyle;
            if (si == null)
                return;

            if (ctlCAlign != null)
                ctlCAlign.Checked = si.TextAlign == TextAlignEnum.Center ? true : false;
            if (ctlLAlign != null)
                ctlLAlign.Checked = si.TextAlign == TextAlignEnum.Left ? true : false;
            if (ctlRAlign != null)
                ctlRAlign.Checked = si.TextAlign == TextAlignEnum.Right ? true : false;
            if (ctlBold != null)
                ctlBold.Checked = si.IsFontBold() ? true : false;
            if (ctlItalic != null)
                ctlItalic.Checked = si.FontStyle == FontStyleEnum.Italic ? true : false;
            if (ctlUnderline != null)
                ctlUnderline.Checked = si.TextDecoration == TextDecorationEnum.Underline ? true : false;
            if (ctlFont != null)
                ctlFont.Text = si.FontFamily;
            if (ctlFontSize != null)
            {
                string rs = string.Format(NumberFormatInfo.InvariantInfo, "{0:0.#}", si.FontSize);
                ctlFontSize.Text = rs;
            }
            if (ctlForeColor != null)
            {
                ctlForeColor.Text = si.Color.IsEmpty ? si.ColorText : ColorTranslator.ToHtml(si.Color);
            }
            if (ctlBackColor != null)
            {
                ctlBackColor.Text = si.BackgroundColor.IsEmpty ? si.BackgroundColorText : ColorTranslator.ToHtml(si.BackgroundColor);
            }

            bSuppressChange = false;
        }

        private void tsmData_Popup(object sender, EventArgs ea)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            bool bEnable = false;
            if (mc != null && mc.DesignTab == "design")
                bEnable = true;

            this.tsmDataSources.Enabled = this.tsmDataSet.Enabled = this.tsmEmbeddedImages.Enabled = bEnable;
            if (!bEnable)
                return;

            // Run thru all the existing DataSets
            tsmDataSet.DropDownItems.Clear();
            ToolStripMenuItem tsmNew = new ToolStripMenuItem("New...");
            tsmNew.Click+=new EventHandler(this.tsmDataSets_Click);
            tsmDataSet.DropDownItems.Add(tsmNew);

            DesignXmlDraw draw = mc.DrawCtl;
            XmlNode rNode = draw.GetReportNode();
            XmlNode dsNode = draw.GetNamedChildNode(rNode, "DataSets");
            if (dsNode != null)
            {
                foreach (XmlNode dNode in dsNode)
                {
                    if (dNode.Name != "DataSet")
                        continue;
                    XmlAttribute nAttr = dNode.Attributes["Name"];
                    if (nAttr == null)	// shouldn't really happen
                        continue;
                    ToolStripMenuItem tsm = new ToolStripMenuItem(nAttr.Value);
                    tsm.Click += new EventHandler(this.tsmDataSets_Click);
                    tsmDataSet.DropDownItems.Add(tsm);
                   
                }
            }
        }

        private void tsmDataSources_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.StartUndoGroup("DataSources Dialog");
            using (DialogDataSources dlgDS = new DialogDataSources(mc.SourceFile, mc.DrawCtl))
            {
                dlgDS.StartPosition = FormStartPosition.CenterParent;
                DialogResult dr = dlgDS.ShowDialog();
                mc.Editor.EndUndoGroup(dr == DialogResult.OK);
                if (dr == DialogResult.OK)
                    mc.Modified = true;
            }
        }

        private void tsmDataSets_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null || mc.DrawCtl == null || mc.ReportDocument == null)
                return;

            ToolStripMenuItem tsm = sender as ToolStripMenuItem;
            if (tsm == null)
                return;
            mc.Editor.StartUndoGroup("DataSet Dialog");

            string dsname = tsm.Text;

            // Find the dataset we need
            List<XmlNode> ds = new List<XmlNode>();
            DesignXmlDraw draw = mc.DrawCtl;
            XmlNode rNode = draw.GetReportNode();
            XmlNode dsNode = draw.GetCreateNamedChildNode(rNode, "DataSets");
            XmlNode dataset = null;

            // find the requested dataset: the tsm text equals the name of the dataset
            int dsCount = 0;		// count of the datasets
            string onlyOneDsname = null;
            foreach (XmlNode dNode in dsNode)
            {
                if (dNode.Name != "DataSet")
                    continue;
                XmlAttribute nAttr = dNode.Attributes["Name"];
                if (nAttr == null)	// shouldn't really happen
                    continue;
                if (dsCount == 0)
                    onlyOneDsname = nAttr.Value;		// we keep track of 1st name; 

                dsCount++;
                if (nAttr.Value == dsname)
                    dataset = dNode;
            }

            bool bNew = false;
            if (dataset == null)	// This must be the new tsm item
            {
                dataset = draw.CreateElement(dsNode, "DataSet", null);	// create empty node
                bNew = true;
            }
            ds.Add(dataset);

            using (PropertyDialog pd = new PropertyDialog(mc.DrawCtl, ds, PropertyTypeEnum.DataSets))
            {
                DialogResult dr = pd.ShowDialog();
                if (pd.Changed || dr == DialogResult.OK)
                {
                    if (dsCount == 1)
                    // if we used to just have one DataSet we may need to fix up DataRegions 
                    //	that were defaulting to that name
                    {
                        dsCount = 0;
                        bool bUseName = false;
                        foreach (XmlNode dNode in dsNode)
                        {
                            if (dNode.Name != "DataSet")
                                continue;
                            XmlAttribute nAttr = dNode.Attributes["Name"];
                            if (nAttr == null)	// shouldn't really happen
                                continue;

                            dsCount++;
                            if (onlyOneDsname == nAttr.Value)
                                bUseName = true;
                        }
                        if (bUseName && dsCount > 1)
                        {
                            foreach (XmlNode drNode in draw.ReportNames.ReportItems)
                            {
                                switch (drNode.Name)
                                {
                                    // If a DataRegion doesn't have a dataset name specified use previous one
                                    case "Table":
                                    case "List":
                                    case "Matrix":
                                    case "Chart":
                                        XmlNode aNode = draw.GetNamedChildNode(drNode, "DataSetName");
                                        if (aNode == null)
                                            draw.CreateElement(drNode, "DataSetName", onlyOneDsname);
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                    }
                    mc.Modified = true;
                }
                else if (bNew)	// if canceled and new DataSet get rid of temp node
                {
                    dsNode.RemoveChild(dataset);
                }
                if (pd.Delete)	// user must have hit a delete button for this to get set
                    dsNode.RemoveChild(dataset);

                if (!dsNode.HasChildNodes)		// If no dataset exists we remove DataSets
                    draw.RemoveElement(rNode, "DataSets");

                mc.Editor.EndUndoGroup(pd.Changed || dr == DialogResult.OK);
            }
        }

        private void tsmEmbeddedImages_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.StartUndoGroup("Embedded Images Dialog");
            using (DialogEmbeddedImages dlgEI = new DialogEmbeddedImages(mc.DrawCtl))
            {
                dlgEI.StartPosition = FormStartPosition.CenterParent;
                DialogResult dr = dlgEI.ShowDialog();
                mc.Editor.EndUndoGroup(dr == DialogResult.OK);
                if (dr == DialogResult.OK)
                    mc.Modified = true;
            }
        }

        private void tsmFileNewDataSourceRef_Click(object sender, System.EventArgs e)
        {
            using (DialogDataSourceRef dlgDS = new DialogDataSourceRef())
            {
                dlgDS.StartPosition = FormStartPosition.CenterParent;
                dlgDS.ShowDialog();
                if (dlgDS.DialogResult == DialogResult.Cancel)
                    return;
            }
        }

        private void tsmFileNewReport_Click(object sender, System.EventArgs e)
        {
            using (DialogDatabase dlgDB = new DialogDatabase(this))
            {
                dlgDB.StartPosition = FormStartPosition.CenterParent;
                dlgDB.FormBorderStyle = FormBorderStyle.SizableToolWindow;

                // show modally
                dlgDB.ShowDialog();
                if (dlgDB.DialogResult == DialogResult.Cancel)
                    return;
                string rdl = dlgDB.ResultReport;

                // Create the MDI child using the RDL syntax the wizard generates
                MDIChild mc = CreateMDIChild(null, rdl, false);
                mc.Modified = true;
                // Force building of report names for new reports
                if (mc.DrawCtl.ReportNames == null) { }
            }
        }

        private void tsmFilePrint_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;
            if (printChild != null)			// already printing
            {
                MessageBox.Show("Can only print one file at a time.", "RDL Design");
                return;
            }

            printChild = mc;

            PrintDocument pd = new PrintDocument();
            pd.DocumentName = mc.SourceFile;
            pd.PrinterSettings.FromPage = 1;
            pd.PrinterSettings.ToPage = mc.PageCount;
            pd.PrinterSettings.MaximumPage = mc.PageCount;
            pd.PrinterSettings.MinimumPage = 1;
            pd.DefaultPageSettings.Landscape = mc.PageWidth > mc.PageHeight ? true : false;

            using (PrintDialog dlg = new PrintDialog())
            {
                dlg.Document = pd;
                dlg.AllowSelection = true;
                dlg.AllowSomePages = true;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        if (pd.PrinterSettings.PrintRange == PrintRange.Selection)
                        {
                            pd.PrinterSettings.FromPage = mc.PageCurrent;
                        }
                        mc.Print(pd);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Print error: " + ex.Message, "RDL Design");
                    }
                }
                printChild = null;
            }
        }

        private void tsmFileSave_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            if (!mc.FileSave())
                return;

            NoteRecentFiles(mc.SourceFile, true);

            if (mc.Editor != null)
                mc.Editor.ClearUndo();

            return;
        }

        private void tsmExportCsv_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Export("csv");
            return;
        }

        private void tsmExportExcel_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Export("Excel");
            return;
        }

        private void tsmExportRtf_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Export("rtf");
            return;
        }

        private void tsmExportXml_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Export("dmp");//xml
            return;
        }

        private void tsmExportHtml_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Export("html");
            return;
        }

        private void tsmExportMHtml_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Export("mht");
            return;
        }

        private void tsmExportPdf_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Export("pdf");
            return;
        }

        private void tsmExportTif_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Export("tif");
            return;
        }

        private void tsmFileSaveAs_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            if (!mc.FileSaveAs())
                return;

            mc.Viewer.Folder = Path.GetDirectoryName(mc.SourceFile);
            mc.Viewer.ReportName = Path.GetFileNameWithoutExtension(mc.SourceFile);
            mc.Text = Path.GetFileName(mc.SourceFile);

            NoteRecentFiles(mc.SourceFile, true);

            if (mc.Editor != null)
                mc.Editor.ClearUndo();

            return;
        }

        private void tsmEdit_Popup(object sender, EventArgs ea)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            // These tsms require an MDIChild in order to work
            RdlEditPreview e = mc == null ? null : mc.RdlEditor;
            //bool bNotPreview = true;
            //tsmEdit.DropDownItems.Clear();
            //if (e == null || e.DesignTab != "edit")
            //{
            //    tsmEditUndo.Text = e == null ? "Undo" : "Undo " + e.UndoDescription;
            //    if (e != null && e.DesignTab == "preview")
            //    {
            //        bNotPreview = false;
            //        tsmEdit.DropDownItems.AddRange(
            //            new ToolStripMenuItem[] {  tsmEditUndo, /* tsmEditRedo,*/ tsmFSep1,  tsmEditCut, tsmEditCopy,
            //                           tsmEditPaste, tsmEditDelete, tsmFSep2, tsmEditFind, tsmFSep3, tsmEditSelectAll});
            //        tsmEditFind.Enabled = true;
            //    }
            //    else
            //        tsmEdit.DropDownItems.AddRange(
            //            new ToolStripMenuItem[] {  tsmEditUndo, /* tsmEditRedo,*/ tsmFSep1,  tsmEditCut, tsmEditCopy,
            //                           tsmEditPaste, tsmEditDelete, tsmFSep2, tsmEditSelectAll});

            //    if (mc == null || e == null)
            //    {
            //        tsmEditUndo.Enabled = tsmEditRedo.Enabled = tsmEditCut.Enabled = tsmEditCopy.Enabled =
            //            tsmEditPaste.Enabled = tsmEditDelete.Enabled = tsmEditSelectAll.Enabled =
            //            tsmEditFind.Enabled = false;
            //        return;
            //    }
            //}
            //else
            //{
            //    tsmEditUndo.Text = "Undo";
            //    tsmEdit.DropDownItems.AddRange(
            //        new ToolStripMenuItem[] { tsmEditUndo, tsmEditRedo, tsmFSep1, tsmEditCut, tsmEditCopy,
            //                           tsmEditPaste, tsmEditDelete, tsmFSep2, tsmEditSelectAll, tsmFSep3,
            //                           tsmEditFind, tsmEditFindNext, tsmEditReplace, tsmEditGoto,
            //                           tsmFSep4, tsmEditFormatXml});

            //    bool bAnyText = e.Text.Length > 0;			// any text to search at all?
            //    tsmEditFind.Enabled = tsmEditFindNext.Enabled =
            //        tsmEditReplace.Enabled = tsmEditGoto.Enabled = bAnyText;
            //}
            //tsmEditUndo.Enabled = e.CanUndo && bNotPreview;
            //tsmEditRedo.Enabled = e.CanRedo && bNotPreview;
            //bool bSelection = e.SelectionLength > 0;	// any text selected?
            //tsmEditCut.Enabled = bSelection && bNotPreview;
            //tsmEditCopy.Enabled = bSelection;
            //tsmEditPaste.Enabled = Clipboard.GetDataObject().GetDataPresent(DataFormats.Text) && bNotPreview;
            //tsmEditDelete.Enabled = bSelection && bNotPreview;
            //tsmEditSelectAll.Enabled = bNotPreview;

        }

        private void tsmEditUndo_Click(object sender, System.EventArgs ea)
        {
            if (this.ctlEditTextbox != null && ctlEditTextbox.Focused)
            {
                ctlEditTextbox.Undo();
                return;
            }

            RdlEditPreview e = GetEditor();
            if (e == null)
                return;

            if (e.CanUndo == true)
            {
                e.Undo();

                MDIChild mc = this.ActiveMdiChild as MDIChild;
                if (mc != null && mc.DesignTab == "design")
                {
                    e.DesignCtl.SetScrollControls();
                }
                this.SelectionChanged(this, new EventArgs());
            }
        }

        private void tsmEditRedo_Click(object sender, System.EventArgs ea)
        {
            RdlEditPreview e = GetEditor();
            if (e == null)
                return;

            if (e.CanRedo == true)
            {
                e.Redo();
            }
        }

        private void tsmEditCut_Click(object sender, System.EventArgs ea)
        {
            if (this.ctlEditTextbox != null && ctlEditTextbox.Focused)
            {
                ctlEditTextbox.Cut();
                return;
            }

            RdlEditPreview e = GetEditor();
            if (e == null)
                return;

            if (e.SelectionLength > 0)
                e.Cut();
        }

        private void tsmEditCopy_Click(object sender, System.EventArgs ea)
        {
            if (this.ctlEditTextbox != null && ctlEditTextbox.Focused)
            {
                ctlEditTextbox.Copy();
                return;
            }
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            RdlEditPreview e = mc.RdlEditor;
            if (e == null)
                return;

            if (e.SelectionLength > 0)
                e.Copy();
        }

        private void tsmEditPaste_Click(object sender, System.EventArgs ea)
        {
            if (this.ctlEditTextbox != null && ctlEditTextbox.Focused)
            {
                ctlEditTextbox.Paste();
                return;
            }

            RdlEditPreview e = GetEditor();
            if (e == null)
                return;

            if (Clipboard.GetDataObject().GetDataPresent(DataFormats.Text) == true ||
                Clipboard.GetDataObject().GetDataPresent(DataFormats.Bitmap) == true)
                e.Paste();
        }

        private void tsmEditDelete_Click(object sender, System.EventArgs ea)
        {
            RdlEditPreview e = GetEditor();
            if (e == null)
                return;

            if (e.SelectionLength > 0)
                e.SelectedText = "";
        }

        private void tsmEditProperties_Click(object sender, System.EventArgs ea)
        {
            //RdlEditPreview e = GetEditor();
            //if (e == null)
            //    return;

            //e.DesignCtl.tsmProperties_Click();
            ShowProperties(!_ShowProperties);
        }

        internal void ShowProperties(bool bShow)
        {
            _ShowProperties = bShow;
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null || !_ShowProperties || mc.DesignTab != "design")
                mainProperties.ResetSelection(null, null);
            else
                mainProperties.ResetSelection(mc.RdlEditor.DrawCtl, mc.RdlEditor.DesignCtl);

            if (mc != null && !_ShowProperties)
                mc.SetFocus();
            mainProperties.Visible = mainSp.Visible = _ShowProperties;
            tsmPropertyWindows.Checked = _ShowProperties;
        }

        private void tsmEditSelectAll_Click(object sender, System.EventArgs ea)
        {
            if (this.ctlEditTextbox != null && ctlEditTextbox.Focused)
            {
                ctlEditTextbox.SelectAll();
                return;
            }
            RdlEditPreview e = GetEditor();
            if (e == null)
                return;

            e.SelectAll();
        }

        private void tsmEditFind_Click(object sender, System.EventArgs ea)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            // These tsms require an MDIChild in order to work
            RdlEditPreview e = mc == null ? null : mc.RdlEditor;

            if (e == null)
                return;
            if (e.DesignTab == "preview")
            {
                if (!e.PreviewCtl.ShowFindPanel)
                    e.PreviewCtl.ShowFindPanel = true;
                e.PreviewCtl.FindNext();
            }
            else
            {
                FindTab tab = new FindTab(e);
                tab.Show();
            }
        }

        private void tsmEditFindNext_Click(object sender, System.EventArgs ea)
        {
            RdlEditPreview e = GetEditor();
            if (e == null)
                return;

            FindTab tab = new FindTab(e);
            tab.Show();
        }

        private void tsmEdit_FormatXml(object sender, System.EventArgs ea)
        {
            RdlEditPreview e = GetEditor();
            if (e == null)
                return;

            if (e.Text.Length > 0)
            {
                try
                {
                    e.Text = DesignerUtility.FormatXml(e.Text);
                    e.Modified = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Format XML");
                }
            }
        }

        private void tsmEditReplace_Click(object sender, System.EventArgs ea)
        {
            RdlEditPreview e = GetEditor();
            if (e == null)
                return;
            FindTab tab = new FindTab(e);
            tab.tcFRG.SelectedTab = tab.tabReplace;
            tab.Show();
        }

        private void tsmEditGoto_Click(object sender, System.EventArgs ea)
        {
            RdlEditPreview e = GetEditor();
            if (e == null)
                return;

            FindTab tab = new FindTab(e);
            tab.tcFRG.SelectedTab = tab.tabGoTo;
            tab.Show();
        }

        private void tsmHelpAbout_Click(object sender, System.EventArgs ea)
        {

        }

        private void tsmHelpHelp_Click(object sender, System.EventArgs ea)
        {
            try
            {
                System.Diagnostics.Process.Start(HelpUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n\n" + "Resetting Help URL to default.", "Help URL Invalid");
                _HelpUrl = DefaultHelpUrl;
            }
        }

        private void tsmHelpSupport_Click(object sender, System.EventArgs ea)
        {
            try
            {
                System.Diagnostics.Process.Start(SupportUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n\n" + "Resetting Support URL to default.", "Support URL Invalid");
                _SupportUrl = DefaultSupportUrl;
            }
        }

        internal RdlEditPreview GetEditor()
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return null;
            return mc.Editor;
        }

        private void tsmTools_Popup(object sender, EventArgs e)
        {
            // If the server process isn't running then we'll start it up
            if (_ServerProcess != null && _ServerProcess.HasExited)
                _ServerProcess = null;
            //tsmToolsProcess.Text = this._ServerProcess == null ? "Start Desktop" : "Stop Desktop";

            //MDIChild mc = this.ActiveMdiChild as MDIChild;
            //this.tsmToolsValidateSchema.Enabled = (mc != null && mc.DesignTab == "edit");

        }

        private void tsmToolsProcess_Click(object sender, EventArgs e)
        {
            if (_ServerProcess == null)
                tsmToolsStartProcess(true);
            else
                tsmToolsCloseProcess(true);
        }

        internal void tsmToolsStartProcess(bool bMsg)
        {
            if (_ServerProcess != null && !_ServerProcess.HasExited)
                return;

            string pswd = GetPassword();

            try
            {
                string filename = string.Format("{0}{1}",
                    AppDomain.CurrentDomain.BaseDirectory, "RdlDesktop.exe");

                ProcessStartInfo psi = new ProcessStartInfo(filename);
                if (pswd != null)
                    psi.Arguments = "/p" + pswd;
                psi.RedirectStandardError = psi.RedirectStandardInput = psi.RedirectStandardOutput = true;
                psi.UseShellExecute = false;
                //psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.CreateNoWindow = true;
                _ServerProcess = Process.Start(psi);
            }
            catch (Exception ex)
            {
                if (bMsg)
                    MessageBox.Show(ex.Message, "Unable to start Desktop");
            }

            return;
        }

        internal void tsmToolsCloseProcess(bool bMsg)
        {
            if (_ServerProcess == null)
                return;
            if (!_ServerProcess.HasExited)
            {
                try
                {
                    _ServerProcess.StandardInput.WriteLine("x");	// x stops the server
                }
                catch (Exception ex)
                {
                    if (bMsg)
                        MessageBox.Show(ex.Message, "Error stopping process");
                }
            }
            _ServerProcess = null;
        }

        private void tsmToolsOptions_Click(object sender, EventArgs e)
        {
            using (DialogToolOptions dlg = new DialogToolOptions(this))
            {
                DialogResult rc = dlg.ShowDialog();
            }
        }

        private void tsmToolsValidateSchema_Click(object sender, EventArgs e)
        {
            if (_ValidateRdl == null)
            {
                _ValidateRdl = new DialogValidateRdl(this);
                _ValidateRdl.Show();
            }
            else
                _ValidateRdl.BringToFront();
            return;
        }

        internal void ValidateSchemaClosing()
        {
            this._ValidateRdl = null;
        }

        private void tsmWnd_Popup(object sender, EventArgs e)
        {
            //// These tsms require an MDIChild in order to work
            //bool bEnable = this.MdiChildren.Length > 0 ? true : false;

            //tsmCascade.Enabled = bEnable;
            //tsmTile.Enabled = bEnable;
            //tsmCloseAll.Enabled = bEnable;
        }

        private void tsmWndCascade_Click(object sender, EventArgs e)
        {
            this.LayoutMdi(MdiLayout.Cascade);
        }

        private void tsmWndCloseAll_Click(object sender, EventArgs e)
        {
            foreach (Form f in this.MdiChildren)
            {
                f.Close();
            }
        }

        private void tsmWndCloseAllButCurrent_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            foreach (Form f in this.MdiChildren)
            {
                if (mc == f as MDIChild)
                    continue;
                f.Close();
            }
            return;
        }

        private void tsmWndTileH_Click(object sender, EventArgs e)
        {
            this.LayoutMdi(MdiLayout.TileHorizontal);
        }

        private void tsmWndTileV_Click(object sender, EventArgs e)
        {
            this.LayoutMdi(MdiLayout.TileVertical);
        }

        private void tsmRecentItem_Click(object sender, System.EventArgs e)
        {
            ToolStripMenuItem m = (ToolStripMenuItem)sender;
            int si = m.Text.IndexOf(" ");
            string file = m.Text.Substring(si + 1);

            CreateMDIChild(file, null, true);
        }

        private void RdlDesigner_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveStartupState();
            tsmToolsCloseProcess(false);
            CleanupTempFiles();
        }

        private void NoteRecentFiles(string name, bool bResetMenu)
        {
            if (name == null)
                return;

            name = name.Trim();
            if (_RecentFiles.ContainsValue(name))
            {	// need to move it to top of list; so remove old one
                int loc = _RecentFiles.IndexOfValue(name);
                _RecentFiles.RemoveAt(loc);
            }
            if (_RecentFiles.Count >= _RecentFilesMax)
            {
                _RecentFiles.RemoveAt(0);	// remove the first entry
            }
            _RecentFiles.Add(DateTime.Now, name);
            if (bResetMenu)
                RecentFilesMenu();
            return;
        }

        internal void RecentFilesMenu()
        {
            tsmRecentFiles.DropDownItems.Clear();
            int mi = 1;
            for (int i = _RecentFiles.Count - 1; i >= 0; i--)
            {
                string tsmText = string.Format("&{0} {1}", mi++, _RecentFiles.Values[i]);
                ToolStripMenuItem m = new ToolStripMenuItem(tsmText);
                m.Click += new EventHandler(this.tsmRecentItem_Click);
                tsmRecentFiles.DropDownItems.Add(m);
            }
        }

        internal void ResetPassword()
        {
            bGotPassword = false;
            _DataSourceReferencePassword = null;
        }

        internal string GetPassword()
        {
            if (bGotPassword)
                return _DataSourceReferencePassword;

            using (DataSourcePassword dlg = new DataSourcePassword())
            {
                DialogResult rc = dlg.ShowDialog();
                bGotPassword = true;
                if (rc == DialogResult.OK)
                    _DataSourceReferencePassword = dlg.PassPhrase;

                return _DataSourceReferencePassword;
            }
        }

        static internal int[] GetCustomColors()
        {
            string optFileName = AppDomain.CurrentDomain.BaseDirectory + "designerstate.xml";
            int white = 16777215;	// default to white (magic number)
            int[] cArray = new int[] {white, white, white, white,white, white, white, white,
								    white, white, white, white, white, white, white, white};
            try
            {
                XmlDocument xDoc = new XmlDocument();
                xDoc.PreserveWhitespace = false;
                xDoc.Load(optFileName);
                XmlNode xNode;
                xNode = xDoc.SelectSingleNode("//designerstate");

                string tcolors = "";
                // Loop thru all the child nodes
                foreach (XmlNode xNodeLoop in xNode.ChildNodes)
                {
                    if (xNodeLoop.Name != "CustomColors")
                        continue;
                    tcolors = xNodeLoop.InnerText;
                    break;
                }
                string[] colorList = tcolors.Split(',');
                int i = 0;

                foreach (string c in colorList)
                {
                    try { cArray[i] = int.Parse(c); }
                    catch { cArray[i] = white; }
                    i++;
                    if (i >= cArray.Length)		// Only allow 16 custom colors
                        break;
                }
            }
            catch
            {		// Didn't sucessfully get the startup state but don't really care
            }
            return cArray;
        }

        static internal void SetCustomColors(int[] colors)
        {
            string optFileName = AppDomain.CurrentDomain.BaseDirectory + "designerstate.xml";

            StringBuilder sb = new StringBuilder();
            foreach (int c in colors)
            {
                sb.Append(c.ToString());
                sb.Append(",");
            }

            sb.Remove(sb.Length - 1, 1);	// remove last ","
            try
            {
                XmlDocument xDoc = new XmlDocument();
                xDoc.PreserveWhitespace = false;
                xDoc.Load(optFileName);
                XmlNode xNode;
                xNode = xDoc.SelectSingleNode("//designerstate");

                // Loop thru all the child nodes
                XmlNode cNode = null;
                foreach (XmlNode xNodeLoop in xNode.ChildNodes)
                {
                    if (xNodeLoop.Name == "CustomColors")
                    {
                        cNode = xNodeLoop;
                        break;
                    }
                }

                if (cNode == null)
                {
                    cNode = xDoc.CreateElement("CustomColors");
                    xNode.AppendChild(cNode);
                }

                cNode.InnerText = sb.ToString();

                xDoc.Save(optFileName);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Custom Color Save Failed");
            }
            return;
        }

        private void EditTextbox_Validated(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null ||
                mc.DesignTab != "design" || mc.DrawCtl.SelectedCount != 1 ||
                mc.Editor == null)
                return;

            mc.Editor.SetSelectedText(ctlEditTextbox.Text);
            SetProperties(mc);
        }

        private void Insert_Click(object sender, EventArgs e)
        {
            if (ctlInsertCurrent != null)
                ctlInsertCurrent.Checked = false;

            SimpleToggle ctl = (SimpleToggle)sender;
            ctlInsertCurrent = ctl.Checked ? ctl : null;

            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;
            mc.SetFocus();

            mc.CurrentInsert = ctlInsertCurrent == null ? null : (string)ctlInsertCurrent.Tag;
        }

        private void ctlBold_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.ApplyStyleToSelected("FontWeight", ctlBold.Checked ? "Bold" : "Normal");
            SetProperties(mc);

            SetMDIChildFocus(mc);
        }

        private void ctlItalic_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.ApplyStyleToSelected("FontStyle", ctlItalic.Checked ? "Italic" : "Normal");
            SetProperties(mc);

            SetMDIChildFocus(mc);
        }

        private void ctlUnderline_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.ApplyStyleToSelected("TextDecoration", ctlUnderline.Checked ? "Underline" : "None");
            SetProperties(mc);

            SetMDIChildFocus(mc);
        }

        private void ctlForeColor_Change(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            if (!bSuppressChange)
            {
                mc.ApplyStyleToSelected("Color", ctlForeColor.Text);
                SetProperties(mc);
            }
            SetMDIChildFocus(mc);
        }

        private void ctlBackColor_Change(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;
            if (!bSuppressChange)
            {
                mc.ApplyStyleToSelected("BackgroundColor", ctlBackColor.Text);
                SetProperties(mc);
            }

            SetMDIChildFocus(mc);
        }

        private void ctlFont_Change(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            if (!bSuppressChange)
            {
                mc.ApplyStyleToSelected("FontFamily", ctlFont.Text);
                SetProperties(mc);
            }
            SetMDIChildFocus(mc);
        }

        private void ctlFontSize_Change(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            if (!bSuppressChange)
            {
                mc.ApplyStyleToSelected("FontSize", ctlFontSize.Text + "pt");
                SetProperties(mc);
            }
            SetMDIChildFocus(mc);
        }

        private void ctlSelectTool_Click(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.SelectionTool = ctlSelectTool.Checked;

            SetMDIChildFocus(mc);
        }

        private void ctlZoom_Change(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;
            mc.SetFocus();

            switch (ctlZoom.Text)
            {
                case "Actual Size":
                    mc.Zoom = 1;
                    break;
                case "Fit Page":
                    mc.ZoomMode = ZoomEnum.FitPage;
                    break;
                case "Fit Width":
                    mc.ZoomMode = ZoomEnum.FitWidth;
                    break;
                default:
                    string s = ctlZoom.Text.Substring(0, ctlZoom.Text.Length - 1);
                    float z;
                    try
                    {
                        z = Convert.ToSingle(s) / 100f;
                        mc.Zoom = z;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Zoom Value Invalid");
                    }
                    break;
            }
        }

        private void RdlDesigner_MdiChildActivate(object sender, EventArgs e)
        {
            if (this._ValidateRdl != null)		// don't keep the validation open when window changes
                this._ValidateRdl.Close();

            DesignTabChanged(sender, e);
            SelectionChanged(sender, e);
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;
            mc.SetFocus();
            if (mc.Tab != null)
                mainTC.SelectTab(mc.Tab);
        }

        private void SetMDIChildFocus(MDIChild mc)
        {
            // We don't want to be triggering any change events when the focus is changing
            bool bSuppress = bSuppressChange;
            bSuppressChange = true;
            mc.SetFocus();
            bSuppressChange = bSuppress;
        }

        private void SetProperties(MDIChild mc)
        {
            if (mc == null || !_ShowProperties || mc.DesignTab != "design")
                mainProperties.ResetSelection(null, null);
            else
                mainProperties.ResetSelection(mc.RdlEditor.DrawCtl, mc.RdlEditor.DesignCtl);
        }

        private void SetStatusNameAndPosition()
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;

            SetProperties(mc);

            if (mc == null)
            {
                statusPosition.Text = statusSelected.Text = "";
            }
            else if (mc.DesignTab == "design")
                SetStatusNameAndPositionDesign(mc);
            else if (mc.DesignTab == "edit")
                SetStatusNameAndPositionEdit(mc);
            else
            {
                statusPosition.Text = statusSelected.Text = "";
            }
            return;
        }

        private void SetStatusNameAndPositionDesign(MDIChild mc)
        {
            if (mc.DrawCtl.SelectedCount <= 0)
            {
                statusPosition.Text = statusSelected.Text = "";
                return;
            }

            // Handle position
            PointF pos = mc.SelectionPosition;
            SizeF sz = mc.SelectionSize;
            string spos;
            if (pos.X == float.MinValue)	// no item selected is probable cause
                spos = "";
            else
            {
                RegionInfo rinfo = new RegionInfo(CultureInfo.CurrentCulture.LCID);
                double m72 = DesignXmlDraw.POINTSIZED;
                if (rinfo.IsMetric)
                {
                    if (sz.Width == float.MinValue)	// item is in a table/matrix is probably cause
                        spos = string.Format("   x={0:0.00}cm, y={1:0.00}cm        ",
                            pos.X / (m72 / 2.54d), pos.Y / (m72 / 2.54d));
                    else
                        spos = string.Format("   x={0:0.00}cm, y={1:0.00}cm, w={2:0.00}cm, h={3:0.00}cm        ",
                            pos.X / (m72 / 2.54d), pos.Y / (m72 / 2.54d),
                            sz.Width / (m72 / 2.54d), sz.Height / (m72 / 2.54d));
                }
                else
                {
                    if (sz.Width == float.MinValue)
                        spos = string.Format("   x={0:0.00}\", y={1:0.00}\"        ",
                            pos.X / m72, pos.Y / m72);
                    else
                        spos = string.Format("   x={0:0.00}\", y={1:0.00}\", w={2:0.00}\", h={3:0.00}\"        ",
                            pos.X / m72, pos.Y / m72, sz.Width / m72, sz.Height / m72);
                }
            }
            if (spos != statusPosition.Text)
                statusPosition.Text = spos;

            // Handle text
            string sname = mc.SelectionName;
            if (sname != statusSelected.Text)
                statusSelected.Text = sname;
            return;
        }

        private void SetStatusNameAndPositionEdit(MDIChild mc)
        {
            string spos = string.Format("Ln {0}  Ch {1}", mc.CurrentLine, mc.CurrentCh);
            if (spos != statusSelected.Text)
                statusSelected.Text = spos;

            if (statusPosition.Text != "")
                statusPosition.Text = "";

            return;
        }

        private void EditTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            // Force scroll up and down
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    mc.SetFocus();
                    e.Handled = true;
                    break;
                case Keys.Escape:
                    if (mc.DrawCtl.SelectedCount == 1)
                    {
                        XmlNode tn = mc.DrawCtl.SelectedList[0] as XmlNode;
                        if (tn != null && tn.Name == "Textbox")
                        {
                            ctlEditTextbox.Text = mc.DrawCtl.GetElementValue(tn, "Value", "");
                            e.Handled = true;
                        }
                    }
                    break;
                default:
                    break;
            }

        }

        private void tsmFormat_Popup(object sender, EventArgs e)
        {
            //MDIChild mc = this.ActiveMdiChild as MDIChild;

            //// Determine if group operation on selected is currently allowed
            //bool bEnable = (mc != null && mc.DesignTab == "design" && mc.DrawCtl.AllowGroupOperationOnSelected);

            //this.tsmFormatAlignB.Enabled = this.tsmFormatAlignC.Enabled =
            //    this.tsmFormatAlignL.Enabled = this.tsmFormatAlignM.Enabled =
            //    this.tsmFormatAlignR.Enabled = this.tsmFormatAlignT.Enabled =
            //    bEnable;

            //tsmFormatSizeW.Enabled = tsmFormatSizeH.Enabled = tsmFormatSizeB.Enabled = bEnable;

            //tsmFormatHorzE.Enabled = tsmFormatHorzI.Enabled = tsmFormatHorzD.Enabled =
            //    tsmFormatHorzZ.Enabled = bEnable;

            //tsmFormatVertE.Enabled = tsmFormatVertI.Enabled = tsmFormatVertD.Enabled =
            //    tsmFormatVertZ.Enabled = bEnable;

            //bEnable = (mc != null && mc.DesignTab == "design" && mc.DrawCtl.SelectedCount > 0);
            //this.tsmFormatPaddingBottomI.Enabled =
            //    this.tsmFormatPaddingBottomD.Enabled =
            //    this.tsmFormatPaddingBottomZ.Enabled =
            //    this.tsmFormatPaddingTopI.Enabled =
            //    this.tsmFormatPaddingTopD.Enabled =
            //    this.tsmFormatPaddingTopZ.Enabled =
            //    this.tsmFormatPaddingLeftI.Enabled =
            //    this.tsmFormatPaddingLeftD.Enabled =
            //    this.tsmFormatPaddingLeftZ.Enabled =
            //    this.tsmFormatPaddingRightI.Enabled =
            //    this.tsmFormatPaddingRightD.Enabled =
            //    this.tsmFormatPaddingRightZ.Enabled =
            //        bEnable;
        }

        private void tsmFormatAlignButton_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            TextAlignEnum ta = TextAlignEnum.General;

            if (sender == ctlLAlign)
            {
                ta = TextAlignEnum.Left;
                ctlLAlign.Checked = true;
                ctlRAlign.Checked = ctlCAlign.Checked = false;
            }
            else if (sender == ctlRAlign)
            {
                ta = TextAlignEnum.Right;
                ctlRAlign.Checked = true;
                ctlLAlign.Checked = ctlCAlign.Checked = false;
            }
            else if (sender == ctlCAlign)
            {
                ta = TextAlignEnum.Center;
                ctlCAlign.Checked = true;
                ctlRAlign.Checked = ctlLAlign.Checked = false;
            }

            mc.ApplyStyleToSelected("TextAlign", ta.ToString());
            SetProperties(mc);

            SetProperties(mc);
            SetMDIChildFocus(mc);
        }

        private void tsmFormatAlignC_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.DesignCtl.AlignCenters();
            SetProperties(mc);
        }

        private void tsmFormatAlignL_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.DesignCtl.AlignLefts();
            SetProperties(mc);
        }

        private void tsmFormatAlignR_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.DesignCtl.AlignRights();
            SetProperties(mc);
        }

        private void tsmFormatAlignB_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.DesignCtl.AlignBottoms();
            SetProperties(mc);
        }

        private void tsmFormatAlignT_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.DesignCtl.AlignTops();
            SetProperties(mc);
        }

        private void tsmFormatAlignM_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.DesignCtl.AlignMiddles();
            SetProperties(mc);
        }

        private void tsmFormatSizeH_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.DesignCtl.SizeHeights();
            SetProperties(mc);
        }

        private void tsmFormatSizeW_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.DesignCtl.SizeWidths();
            SetProperties(mc);
        }

        private void tsmFormatSizeB_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.DesignCtl.SizeBoth();
            SetProperties(mc);
        }

        private void tsmFormatHorzE_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.DesignCtl.HorzSpacingMakeEqual();
            SetProperties(mc);
        }

        private void tsmFormatHorzI_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.DesignCtl.HorzSpacingIncrease();
            SetProperties(mc);
        }

        private void tsmFormatHorzD_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.DesignCtl.HorzSpacingDecrease();
            SetProperties(mc);
        }

        private void tsmFormatHorzZ_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.DesignCtl.HorzSpacingMakeZero();
            SetProperties(mc);
        }

        private void tsmFormatVertE_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.DesignCtl.VertSpacingMakeEqual();
            SetProperties(mc);
        }

        private void tsmFormatVertI_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.DesignCtl.VertSpacingIncrease();
            SetProperties(mc);
        }

        private void tsmFormatVertD_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.DesignCtl.VertSpacingDecrease();
            SetProperties(mc);
        }

        private void tsmFormatVertZ_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            mc.Editor.DesignCtl.VertSpacingMakeZero();
            SetProperties(mc);
        }

        private void tsmView_Popup(object sender, EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            bool bEnable = mc != null;

            tsmDesigner.Enabled = tsmCode.Enabled =
                tsmPreview.Enabled = bEnable;

            tsmPropertyWindows.Enabled = bEnable && mc.DesignTab == "design";
        }

        private void tsmViewDesigner_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;
            mc.RdlEditor.DesignTab = "design";
        }

        private void tsmViewRDL_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;
            mc.RdlEditor.DesignTab = "edit";
        }

        private void tsmViewBrowser_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            try
            {
                tsmToolsStartProcess(true);		// start desktop if not already up

                DesktopConfig dc = DialogToolOptions.DesktopConfiguration;

                string rdlfile = Path.GetFileNameWithoutExtension(mc.SourceFile) + "_" + (++TEMPRDL_INC).ToString() + TEMPRDL;
                string file;
                if (Path.IsPathRooted(dc.Directory))
                    file = dc.Directory + Path.DirectorySeparatorChar + rdlfile;
                else
                    file = AppDomain.CurrentDomain.BaseDirectory +
                         dc.Directory + Path.DirectorySeparatorChar + rdlfile;

                if (_TempReportFiles == null)
                {
                    _TempReportFiles = new List<string>();
                    _TempReportFiles.Add(file);
                }
                else
                {
                    if (!_TempReportFiles.Contains(file))
                        _TempReportFiles.Add(file);
                }
                StreamWriter sw = File.CreateText(file);
                sw.Write(mc.SourceRdl);
                sw.Close();
                // http://localhost:8080/aReport.rdlc?rs:Format=HTML
                string url = string.Format("http://localhost:{0}/{1}?rd:Format=HTML", dc.Port, rdlfile);
                System.Diagnostics.Process.Start(url);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Show Report");
            }

        }

        private void tsmViewPreview_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;
            mc.RdlEditor.DesignTab = "preview";
        }

        private void tsmFormatPadding_Click(object sender, System.EventArgs e)
        {
            MDIChild mc = this.ActiveMdiChild as MDIChild;
            if (mc == null)
                return;

            ToolStripMenuItem mi = sender as ToolStripMenuItem;

            string padname = null;
            int paddiff = 0;
            //if (mi == tsmFormatPaddingLeftI)
            //{
            //    padname = "PaddingLeft";
            //    paddiff = 4;
            //}
            //else if (mi == tsmFormatPaddingLeftD)
            //{
            //    padname = "PaddingLeft";
            //    paddiff = -4;
            //}
            //else if (mi == tsmFormatPaddingLeftZ)
            //{
            //    padname = "PaddingLeft";
            //    paddiff = 0;
            //}
            //else if (mi == tsmFormatPaddingRightI)
            //{
            //    padname = "PaddingRight";
            //    paddiff = 4;
            //}
            //else if (mi == tsmFormatPaddingRightD)
            //{
            //    padname = "PaddingRight";
            //    paddiff = -4;
            //}
            //else if (mi == tsmFormatPaddingRightZ)
            //{
            //    padname = "PaddingRight";
            //    paddiff = 0;
            //}
            //else if (mi == tsmFormatPaddingTopI)
            //{
            //    padname = "PaddingTop";
            //    paddiff = 4;
            //}
            //else if (mi == tsmFormatPaddingTopD)
            //{
            //    padname = "PaddingTop";
            //    paddiff = -4;
            //}
            //else if (mi == tsmFormatPaddingTopZ)
            //{
            //    padname = "PaddingTop";
            //    paddiff = 0;
            //}
            //else if (mi == tsmFormatPaddingBottomI)
            //{
            //    padname = "PaddingBottom";
            //    paddiff = 4;
            //}
            //else if (mi == tsmFormatPaddingBottomD)
            //{
            //    padname = "PaddingBottom";
            //    paddiff = -4;
            //}
            //else if (mi == tsmFormatPaddingBottomZ)
            //{
            //    padname = "PaddingBottom";
            //    paddiff = 0;
            //}

            if (padname != null)
            {
                mc.Editor.DesignCtl.SetPadding(padname, paddiff);
                SetProperties(mc);
            }

        }

        private void CleanupTempFiles()
        {
            if (_TempReportFiles == null)
                return;
            foreach (string file in _TempReportFiles)
            {
                try
                {	// It's ok for the delete to fail
                    File.Delete(file);
                }
                catch
                { }
            }
            _TempReportFiles = null;
        }

       
    }
}
