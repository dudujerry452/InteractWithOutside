using System;
using System.IO;
using System.Threading;
using ClientPlugin.GUI;
using HarmonyLib;
using Sandbox.Graphics.GUI;
using Shared.Config;
using Shared.Logging;
using Shared.Patches;
using Shared.Plugin;
using VRage.FileSystem;
using VRage.Game;
using VRage.Plugins;

using Sandbox;
using EmptyKeys.UserInterface;
using VRage;

using Sandbox.Game.Entities.Blocks;
using Sandbox.Engine.Utils;

using VRage.Scripting;
using System.Security.Policy;
using System.Reflection;

using System.Collections.Generic;
using System.Threading.Tasks;

using Shared.MyLogging;
using System.Collections;
using System.Text;
using System.Linq;
using VRage.Game.Components;
using Microsoft.CodeAnalysis;
using static System.Net.Mime.MediaTypeNames;


using VRageMath;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Sandbox.Game.EntityComponents;
using VRage.Collections;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using System.Data.SqlClient;

using InteractUtil;

namespace InteractWithOutsideAPI
{
    public class TestClass
    {
        public string content = "Hello, Class";

        public string GetContent
        {
            get { return content; }
        }
    }

    public class InteractService
    {
        private static InteractService m_instance;


        private List<IMyGridProgram> m_gridProgramContainer = new List<IMyGridProgram>();   
        private SocketServer m_socketServer;
        private Logger logger;

        private InteractService()
        {
            logger = new Logger("appint.log");

            m_socketServer = new SocketServer(8899);
            logger.Log("Thread Started");


        }

        ~InteractService()
        {
            // TODO: close socket
        }


        public void Control(IMyGridProgram gridProgram, string identityCode)
        {
            m_gridProgramContainer.Add(gridProgram);
        }

        public static InteractService Instance
        {
            get
            {
                if(m_instance == null)
                {
                    m_instance = new InteractService();
                }
                return m_instance;
            }
        }
    }


}