using System;
using Extensibility;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.CommandBars;
using System.Resources;
using System.Reflection;
using System.Globalization;
using System.Windows.Forms;
using System.Collections;
using System.IO;

namespace ThkDevEnc
{
	/// <summary>用于实现外接程序的对象。</summary>
	/// <seealso class='IDTExtensibility2' />
	public class Connect : IDTExtensibility2, IDTCommandTarget
	{
		/// <summary>实现外接程序对象的构造函数。请将您的初始化代码置于此方法内。</summary>
		public Connect()
		{
		}

		/// <summary>实现 IDTExtensibility2 接口的 OnConnection 方法。接收正在加载外接程序的通知。</summary>
		/// <param term='application'>宿主应用程序的根对象。</param>
		/// <param term='connectMode'>描述外接程序的加载方式。</param>
		/// <param term='addInInst'>表示此外接程序的对象。</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
        {
            _applicationObject = (DTE2)application;
            _addInInstance = (AddIn)addInInst;

            OutputWindow outputWindow = (OutputWindow)_applicationObject.Windows.Item(Constants.vsWindowKindOutput).Object;

            bool bbuildok = true;
            try
            {
                m_OutputWindowPane = outputWindow.OutputWindowPanes.Item("生成");

            }
            catch
            {
                //我们试图查找“工具”一词的本地化版本，但未能找到。
                //  默认值为 en-US 单词，该值可能适用于当前区域性。
                bbuildok = false;
            }

            if (!bbuildok)
            {
                try
                {
                    m_OutputWindowPane = outputWindow.OutputWindowPanes.Item("Build");

                }
                catch
                {
                    //我们试图查找“工具”一词的本地化版本，但未能找到。
                    //  默认值为 en-US 单词，该值可能适用于当前区域性。
                    bbuildok = false;
                }
            }


            // Register for the various build events taht we're hooking in to
            EnvDTE.Events events = _applicationObject.Events;
            m_BuildEvents = (EnvDTE.BuildEvents)events.BuildEvents;
            m_BuildEvents.OnBuildProjConfigBegin += new _dispBuildEvents_OnBuildProjConfigBeginEventHandler(OnBuildProjConfigBegin);
            m_BuildEvents.OnBuildProjConfigDone += new _dispBuildEvents_OnBuildProjConfigDoneEventHandler(OnBuildProjConfigDone);
            m_BuildEvents.OnBuildBegin += new _dispBuildEvents_OnBuildBeginEventHandler(this.OnBuildBegin);
            m_BuildEvents.OnBuildDone += new _dispBuildEvents_OnBuildDoneEventHandler(this.OnBuildDone);

            m_envcfgs = new ThkEnvCfgs();
            if (m_envcfgs.LoadConfig())
            {
                ThkEnvConfig enc = m_envcfgs.GetCurEnvConfig();
                if (enc != null)
                {
                    //if (m_OutputWindowPane != null)
                    //{
                    //    m_OutputWindowPane.OutputString("现在开始自动设置配置信息中的环境变量\n");
                    //}

                    ThkEnvCfgItem[] encitems = enc.EnvConfigItems;
                    foreach (ThkEnvCfgItem ci in encitems)
                    {
                        if (ci.EnvCfgFlatform == null || ci.EnvCfgFlatform.Length == 0 || ci.EnvCfgFlatform == "全部" || ci.EnvCfgFlatform == "")
                        {
                            Environment.SetEnvironmentVariable(ci.EnvCfgItem, ci.EnvCfgValue, EnvironmentVariableTarget.Process);
                            //if (m_OutputWindowPane !=null)
                            //{
                            //    m_OutputWindowPane.OutputString(ci.EnvCfgItem + ":" + ci.EnvCfgValue+"\n");
                            //}
                        }
                    }
                }
                else
                {
                    MessageBox.Show("获取配置中的环境变量信息失败");
                }
            }
            else
            {
                MessageBox.Show("加载配置变量配置文件失败");
            }
            {
                string path = System.Environment.CurrentDirectory;
                Assembly myAssembly = Assembly.GetEntryAssembly();
                if (myAssembly == null)
                    myAssembly = Assembly.GetExecutingAssembly();
                if (myAssembly != null)
                {
                    path = myAssembly.Location;
                    DirectoryInfo dr = new DirectoryInfo(path);
                    path = dr.Parent.FullName;  //当前目录的上一级目录
                }
                Environment.SetEnvironmentVariable("THK_VS_ADDIN_PATH", path, EnvironmentVariableTarget.Process);
            }

            //debug
            ThkVCDirSet vcset = m_envcfgs.GetVCConfig();
            if (vcset != null && vcset.EnvConfigItems.Length < 2)
            {
                //vcset.AddVcConfig("VC6", @"D:\develop\VS60\VC98\", "");
                //vcset.AddVcConfig("VC2003", @"D:\develop\vs2002\Vc7\", "");
                //vcset.AddVcConfig("VC2003", @"D:\develop\vs2003\Vc7\", "");
                //vcset.AddVcConfig("VC2005", @"D:\develop\vs2005\Vc\", "");
                //vcset.AddVcConfig("VC2008", @"D:\develop\vs2008\Vc\", "");
                //vcset.AddVcConfig("VC2010", @"D:\develop\vs2010\Vc\", "");
                //vcset.AddVcConfig("VC2012", @"D:\develop\vs2012\Vc\", "");
                //vcset.AddVcConfig("VC2013", @"D:\develop\vs2013\Vc\", "");


                //m_envcfgs.SaveConfig();
            }
            //end of debug

            if (connectMode == ext_ConnectMode.ext_cm_CommandLine || connectMode == ext_ConnectMode.ext_cm_AfterStartup || connectMode == ext_ConnectMode.ext_cm_UISetup ||
                connectMode == ext_ConnectMode.ext_cm_Startup)
            {
                object[] contextGUIDS = new object[] { };
                Commands2 commands = (Commands2)_applicationObject.Commands;
                string toolsMenuName;



                try
                {
                    //若要将此命令移动到另一个菜单，则将“工具”一词更改为此菜单的英文版。
                    //  此代码将获取区域性，将其追加到菜单名中，然后将此命令添加到该菜单中。
                    //  您会在此文件中看到全部顶级菜单的列表
                    //  CommandBar.resx.
                    string resourceName;
                    ResourceManager resourceManager = new ResourceManager("ThkDevEnc.CommandBar", Assembly.GetExecutingAssembly());
                    CultureInfo cultureInfo = new CultureInfo(_applicationObject.LocaleID);

                    if (cultureInfo.TwoLetterISOLanguageName == "zh")
                    {
                        System.Globalization.CultureInfo parentCultureInfo = cultureInfo.Parent;
                        resourceName = String.Concat(parentCultureInfo.Name, "Tools");
                    }
                    else
                    {
                        resourceName = String.Concat(cultureInfo.TwoLetterISOLanguageName, "Tools");
                    }
                    toolsMenuName = resourceManager.GetString(resourceName);
                    if (toolsMenuName == null || toolsMenuName.Length == 0)
                        toolsMenuName = "工具";

                }
                catch
                {
                    //我们试图查找“工具”一词的本地化版本，但未能找到。
                    //  默认值为 en-US 单词，该值可能适用于当前区域性。
                    toolsMenuName = "Tools";
                }

                //将此命令置于“工具”菜单上。
                //查找 MenuBar 命令栏，该命令栏是容纳所有主菜单项的顶级命令栏:
                Microsoft.VisualStudio.CommandBars.CommandBar menuBarCommandBar = ((Microsoft.VisualStudio.CommandBars.CommandBars)_applicationObject.CommandBars)["MenuBar"];

                //在 MenuBar 命令栏上查找“工具”命令栏:
                CommandBarControl toolsControl = menuBarCommandBar.Controls[toolsMenuName];
                CommandBarPopup toolsPopup = (CommandBarPopup)toolsControl;

                //如果希望添加多个由您的外接程序处理的命令，可以重复此 try/catch 块，
                //  只需确保更新 QueryStatus/Exec 方法，使其包含新的命令名。
                //////////////try
                //////////////{
                //////////////    //将一个命令添加到 Commands 集合:
                //////////////    Command command = commands.AddNamedCommand2(_addInInstance, "ProUnlock", "解锁ProE插件", "Executes the command for ProUnlock", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);

                //////////////    //将对应于该命令的控件添加到“工具”菜单:
                //////////////    if ((command != null) && (toolsPopup != null))
                //////////////    {
                //////////////        command.AddControl(toolsPopup.CommandBar, 1);
                //////////////    }
                //////////////}
                //////////////catch (System.ArgumentException)
                //////////////{
                //////////////    //如果出现此异常，原因很可能是由于具有该名称的命令
                //////////////    //  已存在。如果确实如此，则无需重新创建此命令，并且
                //////////////    //  可以放心忽略此异常。
                //////////////}
                try
                {
                    //将一个命令添加到 Commands 集合:
                    Command command = commands.AddNamedCommand2(_addInInstance, "ProeAttach", "调试ProE", "Executes the command for ThkDevEnc", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);

                    //将对应于该命令的控件添加到“工具”菜单:
                    if ((command != null) && (toolsPopup != null))
                    {
                        command.AddControl(toolsPopup.CommandBar, 1);
                    }
                }
                catch (System.ArgumentException)
                {
                    //如果出现此异常，原因很可能是由于具有该名称的命令
                    //  已存在。如果确实如此，则无需重新创建此命令，并且
                    //  可以放心忽略此异常。
                }
                try
                {
                    //将一个命令添加到 Commands 集合:
                    Command command = commands.AddNamedCommand2(_addInInstance, "CatiaAttach", "调试Catia", "Executes the command for ThkDevEnc", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);

                    //将对应于该命令的控件添加到“工具”菜单:
                    if ((command != null) && (toolsPopup != null))
                    {
                        command.AddControl(toolsPopup.CommandBar, 1);
                    }
                }
                catch (System.ArgumentException)
                {
                    //如果出现此异常，原因很可能是由于具有该名称的命令
                    //  已存在。如果确实如此，则无需重新创建此命令，并且
                    //  可以放心忽略此异常。
                }

                try
                {
                    //将一个命令添加到 Commands 集合:
                    Command command = commands.AddNamedCommand2(_addInInstance, "DevEnvCfg", "森科开发环境", "Executes the command for ThkDevEnc", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);

                    //将对应于该命令的控件添加到“工具”菜单:
                    if ((command != null) && (toolsPopup != null))
                    {
                        command.AddControl(toolsPopup.CommandBar, 1);
                    }
                }
                catch (System.ArgumentException)
                {
                    //如果出现此异常，原因很可能是由于具有该名称的命令
                    //  已存在。如果确实如此，则无需重新创建此命令，并且
                    //  可以放心忽略此异常。
                }
                try
                {
                    //将一个命令添加到 Commands 集合:
                    Command command = commands.AddNamedCommand2(_addInInstance, "VCSet", "当前编译环境", "Executes the command for ThkDevEnc", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);

                    //将对应于该命令的控件添加到“工具”菜单:
                    if ((command != null) && (toolsPopup != null))
                    {
                        command.AddControl(toolsPopup.CommandBar, 1);
                    }
                }
                catch (System.ArgumentException)
                {
                    //如果出现此异常，原因很可能是由于具有该名称的命令
                    //  已存在。如果确实如此，则无需重新创建此命令，并且
                    //  可以放心忽略此异常。
                }

                try
                {
                    //将一个命令添加到 Commands 集合:
                    Command command = commands.AddNamedCommand2(_addInInstance, "UnAttach", "全部分离调试", "Executes the command for ThkDevEnc", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);

                    //将对应于该命令的控件添加到“工具”菜单:
                    if ((command != null) && (toolsPopup != null))
                    {
                        command.AddControl(toolsPopup.CommandBar, 1);
                    }
                }
                catch (System.ArgumentException)
                {
                    //如果出现此异常，原因很可能是由于具有该名称的命令
                    //  已存在。如果确实如此，则无需重新创建此命令，并且
                    //  可以放心忽略此异常。
                }


                try
                {
                    //将一个命令添加到 Commands 集合:
                    Command command = commands.AddNamedCommand2(_addInInstance, "CopyFile", "文件拷贝", "Executes the command for ThkDevEnc", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);

                    //将对应于该命令的控件添加到“工具”菜单:
                    if ((command != null) && (toolsPopup != null))
                    {
                        command.AddControl(toolsPopup.CommandBar, 1);
                    }
                }
                catch (System.ArgumentException)
                {
                    //如果出现此异常，原因很可能是由于具有该名称的命令
                    //  已存在。如果确实如此，则无需重新创建此命令，并且
                    //  可以放心忽略此异常。
                }
            }
        }

		/// <summary>实现 IDTExtensibility2 接口的 OnDisconnection 方法。接收正在卸载外接程序的通知。</summary>
		/// <param term='disconnectMode'>描述外接程序的卸载方式。</param>
		/// <param term='custom'>特定于宿主应用程序的参数数组。</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
		{
            if (m_BuildEvents != null)
            {
                m_BuildEvents.OnBuildBegin -= new _dispBuildEvents_OnBuildBeginEventHandler(this.OnBuildBegin);
                m_BuildEvents.OnBuildDone -= new _dispBuildEvents_OnBuildDoneEventHandler(this.OnBuildDone);
                m_BuildEvents.OnBuildProjConfigBegin -= new _dispBuildEvents_OnBuildProjConfigBeginEventHandler(this.OnBuildProjConfigBegin);
                m_BuildEvents.OnBuildProjConfigDone -= new _dispBuildEvents_OnBuildProjConfigDoneEventHandler(this.OnBuildProjConfigDone);
            }

		}

		/// <summary>实现 IDTExtensibility2 接口的 OnAddInsUpdate 方法。当外接程序集合已发生更改时接收通知。</summary>
		/// <param term='custom'>特定于宿主应用程序的参数数组。</param>
		/// <seealso class='IDTExtensibility2' />		
		public void OnAddInsUpdate(ref Array custom)
		{
		}

		/// <summary>实现 IDTExtensibility2 接口的 OnStartupComplete 方法。接收宿主应用程序已完成加载的通知。</summary>
		/// <param term='custom'>特定于宿主应用程序的参数数组。</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnStartupComplete(ref Array custom)
		{
		}

		/// <summary>实现 IDTExtensibility2 接口的 OnBeginShutdown 方法。接收正在卸载宿主应用程序的通知。</summary>
		/// <param term='custom'>特定于宿主应用程序的参数数组。</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnBeginShutdown(ref Array custom)
		{
		}
		
		/// <summary>实现 IDTCommandTarget 接口的 QueryStatus 方法。此方法在更新该命令的可用性时调用</summary>
		/// <param term='commandName'>要确定其状态的命令的名称。</param>
		/// <param term='neededText'>该命令所需的文本。</param>
		/// <param term='status'>该命令在用户界面中的状态。</param>
		/// <param term='commandText'>neededText 参数所要求的文本。</param>
		/// <seealso class='Exec' />
		public void QueryStatus(string commandName, vsCommandStatusTextWanted neededText, ref vsCommandStatus status, ref object commandText)
		{
			if(neededText == vsCommandStatusTextWanted.vsCommandStatusTextWantedNone)
			{
                if (commandName == "ThkDevEnc.Connect.ProUnlock")
				{
					status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported|vsCommandStatus.vsCommandStatusEnabled;
					return;
				}
                if (commandName == "ThkDevEnc.Connect.ProeAttach")
                {
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                    return;
                }
                if (commandName == "ThkDevEnc.Connect.CatiaAttach")
				{
					status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported|vsCommandStatus.vsCommandStatusEnabled;
					return;
				}
                if (commandName == "ThkDevEnc.Connect.DevEnvCfg")
                {
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                    return;
                }
                if(commandName == "ThkDevEnc.Connect.VCSet")
                {
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                    return;
                }
                if (commandName == "ThkDevEnc.Connect.UnAttach")
                {
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                    return;
                }
                if (commandName == "ThkDevEnc.Connect.CopyFile")
                {
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                    return;
                }
            }
		}

		/// <summary>实现 IDTCommandTarget 接口的 Exec 方法。此方法在调用该命令时调用。</summary>
		/// <param term='commandName'>要执行的命令的名称。</param>
		/// <param term='executeOption'>描述该命令应如何运行。</param>
		/// <param term='varIn'>从调用方传递到命令处理程序的参数。</param>
		/// <param term='varOut'>从命令处理程序传递到调用方的参数。</param>
		/// <param term='handled'>通知调用方此命令是否已被处理。</param>
		/// <seealso class='Exec' />
		public void Exec(string commandName, vsCommandExecOption executeOption, ref object varIn, ref object varOut, ref bool handled)
		{
			handled = false;
			if(executeOption == vsCommandExecOption.vsCommandExecOptionDoDefault)
			{
                if (commandName == "ThkDevEnc.Connect.ProeAttach" || commandName == "ThkDevEnc.Connect.CatiaAttach")
				{
					handled = true;
                    bool bCatia = false;
                    if (commandName == "ThkDevEnc.Connect.CatiaAttach")
                        bCatia = true;

                    ArrayList procList = new ArrayList();

                    foreach (Process proc in _applicationObject.Debugger.LocalProcesses)
                    {
                        string strname = proc.Name;
                        strname = strname.ToLower();
                        if (bCatia)
                        {
                            if (strname.Contains("cnext.exe"))
                            {
                                procList.Add(proc);
                            }
                        }
                        else
                        {
                            if (strname.Contains("xtop.exe"))
                            {
                                procList.Add(proc);
                            }
                        }
                    }

                    int iNum = procList.Count;

                    if (iNum > 0)
                    {
                        if (iNum > 1)
                        {
                            SelForm sel = new SelForm();
                            sel.DebugCatia = bCatia;

                            if (sel.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                            {
                                AttachProcess(sel.PROCESSES);
                            }
                        }
                        else
                        {
                            //Engine[] eng = new Engine[2];
                            //Debugger3 dbg = (Debugger3)_applicationObject.Debugger;
                            //Transport tras = dbg.Transports.Item("default");
                            //eng[0] = tras.Engines.Item("native");
                            //eng[1] = tras.Engines.Item("Managed");

                            ((Process)procList[0]).Attach();

                        }
                    }
                    else
                    {
                        if (bCatia)
                            MessageBox.Show("当前没有运行Catia程序");
                        else
                            MessageBox.Show("当前没有运行PROE/CREO程序");

                    }

					return;
				}
                if (commandName == "ThkDevEnc.Connect.ProUnlock")
                {
                    handled = true;

                    ProUnLock pu = new ProUnLock();
                    pu.m_envcfgs = m_envcfgs;
                    pu.ShowDialog();              
                    return;
                }
                if (commandName == "ThkDevEnc.Connect.CopyFile")
                {
                    handled = true;

                    LP_SC.FileCopyForm fm = new LP_SC.FileCopyForm(_applicationObject);
                    fm.ResetTree();
                    fm.ShowDialog();
                    return;
                }
                if (commandName == "ThkDevEnc.Connect.DevEnvCfg")
                {
                    handled = true;

                    EncSet sel = new EncSet();
                    if (DialogResult.OK == sel.ShowDialog())
                    {
                        m_envcfgs.LoadConfig();
                        EnvDTE.Projects curprj = _applicationObject.DTE.Solution.Projects;
                        if(curprj == null || curprj.Count == 0)
                            return;
                        for (int i = 0; i < curprj.Count; i++)
                        {
                            EnvDTE.Project prj = curprj.Item(i);
                           
                        }
                    }

                    return;
                }
                if (commandName == "ThkDevEnc.Connect.VCSet")
                {
                    handled = true;

                    VcInfoSet sel = new VcInfoSet();
                    sel.ShowDialog();

                    return;
                }
                if (commandName == "ThkDevEnc.Connect.UnAttach")
                {
                    handled = true;

                    if (_applicationObject.Debugger.CurrentMode == dbgDebugMode.dbgDesignMode)
                        return;
                    _applicationObject.Debugger.DetachAll();

                    //try
                    //{
                    //    Project prj;
                    //    Configuration config;
                    //    OutputGroups outPGs;
                    //    Properties props;
                    //    if (_applicationObject.Solution.Projects.Count > 0)
                    //    {
                    //        prj = _applicationObject.Solution.Projects.Item(1);
                    //        config = prj.ConfigurationManager.ActiveConfiguration;
                    //        // Return a collection of OutputGroup objects that contain
                    //        // the names of files that are outputs for the project.
                    //        outPGs = config.OutputGroups;
                    //        MessageBox.Show(outPGs.Count.ToString());
                    //        // Returns the project for the config.
                    //        MessageBox.Show(((Project)config.Owner).Name);
                    //        // Returning the platform name for the Configuration.
                    //        MessageBox.Show(config.PlatformName);
                    //        // Returning all properties for Configuration object.
                    //        props = config.Properties;
                    //        string p = "";
                    //        foreach (Property prop in props)
                    //        {
                    //            p = p + prop.Name + "<:>"+prop.Value+"\n";
                    //        }
                    //        MessageBox.Show(p);
                    //    }
                    //}
                    //catch (Exception ex)
                    //{
                    //    MessageBox.Show(ex.Message);
                    //}
                    return;
                }			
            }
		}
        private void AttachProcess(int id)
        {
            foreach (Process proc in _applicationObject.Debugger.LocalProcesses)
            {
                if (proc.ProcessID == id)
                {
                    proc.Attach();
                    break;
                }
            }
        }
        /// <summary>
        /// Called when each project build begins.
        /// 
        /// Record the start time of the project.
        /// </summary>
        void OnBuildProjConfigBegin(string Project, string ProjectConfig, string Platform, string SolutionConfig)
        {
            m_OutputWindowPane.OutputString("\n正准备生成项目 " + Project + ":" + ProjectConfig + ":" + Platform + ":" + SolutionConfig);
            m_OutputWindowPane.OutputString("\n森科开发环境将自动进行编译环境处理......\n");

            if (m_envcfgs == null)
            {
                m_envcfgs = new ThkEnvCfgs();
                m_envcfgs.LoadConfig();
            }
            ThkVCDirSet vcset = m_envcfgs.GetVCConfig();
            if (vcset == null)
            {
                m_OutputWindowPane.OutputString("\n错误：未找到森科编译环境配置信息\n");
                return;
            }
            ThkVCDirItem curVc = new ThkVCDirItem();

            if (ProjectConfig.IndexOf("WF") != -1 || ProjectConfig.IndexOf("VC9") != -1 || ProjectConfig.IndexOf("ThkRelease") != -1)
            {
                curVc = vcset.GetVcItem("VC2008");
            }
            else if (ProjectConfig.IndexOf("CR") != -1)
            {
                curVc = vcset.GetVcItem("VC2010");
            }
            else
            {
                curVc = vcset.GetCurVCItem();
            }
            //if (curVc != null && curVc.StrVCDir != null && curVc.StrVCDir.Length > 0)
            //{
            //    Environment.SetEnvironmentVariable("VCInstallDir", curVc.StrVCDir, EnvironmentVariableTarget.Process);
            //}
            //if (curVc.StrVCVer == null)
            //    curVc.StrVCVer = "未知版本";

           
            //m_OutputWindowPane.OutputString("\n森科开发环境开始进行."+Platform+".平台配置.....\n");
            ThkEnvConfig enc = m_envcfgs.GetCurEnvConfig();
            if (enc != null)
            {
                ThkEnvCfgItem[] encitems = enc.EnvConfigItems;
                foreach (ThkEnvCfgItem ci in encitems)
                {
                    if(String.Compare(ci.EnvCfgFlatform,Platform,true) == 0)
                        Environment.SetEnvironmentVariable(ci.EnvCfgItem, ci.EnvCfgValue, EnvironmentVariableTarget.Process);
                }
            }

            m_OutputWindowPane.OutputString("\n森科开发环境切换.." + curVc.StrVCVer + "....成功！\n");
            string strthkapi = Environment.GetEnvironmentVariable("ThkApi");
            if (strthkapi == null || strthkapi.Length == 0)
                strthkapi = "未配置";
            m_OutputWindowPane.OutputString("\n当前...ThkApi 内容为." + strthkapi + "....\n");
            strthkapi = Environment.GetEnvironmentVariable("ThkProApi");
            if (strthkapi == null || strthkapi.Length == 0)
                strthkapi = "未配置";
            m_OutputWindowPane.OutputString("\n当前...ThkProApi 内容为." + strthkapi + "....\n");
        }

        /// <summary>
        /// Called when each project build completes.  
        /// 
        /// Record the duration of the project by comparing with the start time.
        /// </summary>
        void OnBuildProjConfigDone(string Project, string ProjectConfig, string Platform, string SolutionConfig, bool Success)
        {
            m_OutputWindowPane.OutputString("\n项目:"+Project+",项目配置："+ProjectConfig+"平台："+Platform+"解决方案配置："+SolutionConfig+"\n");
            if (Success)
                m_OutputWindowPane.OutputString("\n编译过程......成功结束！\n");
            else
                m_OutputWindowPane.OutputString("\n编译项目......跨过 或 取消 或 失败！\n");
        }

        /// <summary>
        /// Called when the whole build begins.
        /// 
        /// Record the start time.
        /// </summary>
        public void OnBuildBegin(EnvDTE.vsBuildScope Scope, EnvDTE.vsBuildAction Action)
        {
            // Check for a solution build for Build or RebuildAll
            if ((EnvDTE.vsBuildScope.vsBuildScopeSolution == Scope ||
                EnvDTE.vsBuildScope.vsBuildScopeBatch == Scope || 
                EnvDTE.vsBuildScope.vsBuildScopeProject == Scope) &&
                   (EnvDTE.vsBuildAction.vsBuildActionBuild == Action || EnvDTE.vsBuildAction.vsBuildActionRebuildAll == Action))
            {
                // Flag our build timer
                m_IsBuildOrRebuild = true;

                amTiming = true;
                dtStart = DateTime.Now;

               // m_OutputWindowPane.OutputString("\n森科开发项目编译开始...\n");
                m_OutputWindowPane.OutputString(String.Format("\n现在在开始编译，开始时间为：{0}\n", dtStart));

            }
        }

        /// <summary>
        /// Called when the whole build completes.
        /// 
        /// Calculate and display the total build time.  Also sort the project durations and display them.
        /// </summary>
        public void OnBuildDone(EnvDTE.vsBuildScope Scope, EnvDTE.vsBuildAction Action)
        {
            // Check if we are actually timing this build
            if (amTiming)
            {
                amTiming = false;
                dtEnd = DateTime.Now;
                m_OutputWindowPane.OutputString(String.Format("\n 现在结束编译，开始时间为： {0}，结束时间：{1}\n", dtStart,dtEnd));
                TimeSpan tsElapsed = dtEnd - dtStart;
                m_OutputWindowPane.OutputString(String.Format("\n全部编译时间为： {0}\n", tsElapsed));
            }

            if (m_IsBuildOrRebuild)
            {
                m_IsBuildOrRebuild = false;
                if (m_envcfgs == null)
                {
                    m_envcfgs = new ThkEnvCfgs();
                    m_envcfgs.LoadConfig();

                }
                ThkVCDirSet vcset = m_envcfgs.GetVCConfig();
                if (vcset == null)
                {
                    m_OutputWindowPane.OutputString("\n跨过：未找到森科编译环境配置信息\n");
                    return;
                }
                ThkVCDirItem curVc = vcset.GetCurVCItem();

                if (curVc != null && curVc.StrVCDir!= null && curVc.StrVCDir.Length > 0)
                {
                    Environment.SetEnvironmentVariable("VCInstallDir", curVc.StrVCDir, EnvironmentVariableTarget.Process);
                }
                if (curVc.StrVCVer == null)
                    curVc.StrVCVer = "未知版本";
                m_OutputWindowPane.OutputString("\n森科开发项目编译结束，自动转换成缺省配置.." + curVc.StrVCVer + "....成功！\n");
            }

        }

        ThkEnvCfgs m_envcfgs;
        bool m_IsBuildOrRebuild = false;
        private DTE2 _applicationObject;
        private AddIn _addInInstance;
        private EnvDTE.OutputWindowPane m_OutputWindowPane;
        private EnvDTE.BuildEvents m_BuildEvents;
        private bool amTiming = false;
        private DateTime dtStart, dtEnd;

	}
}