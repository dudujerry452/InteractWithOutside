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

using InteractWithOutsideAPI;

namespace ClientPlugin
{
    [HarmonyPatch(typeof(IVRageScriptingFunctions), "CompileIngameScriptAsync")]
    public static class CompileAsyncPatch
    {

        // Change Programmable block funcition restriction

        [HarmonyPrefix]
        public static bool MyPreFix(this IVRageScripting thiz, string assemblyName, string program, out List<Message> diagnostics, string friendlyName, string typeName, string baseType, ref Task<Assembly> __result)
        {
            Script ingameScript = ingameScript = thiz.GetIngameScript(program, typeName, baseType);
            __result = thiz.CompileAsync(MyApiTarget.Ingame, assemblyName, new Script[1] { ingameScript }, out diagnostics, friendlyName);

            Logger logger = new Logger("appcode.log");
            logger.Log(ingameScript.Code);

            return false;
        }
    }



    [HarmonyPatch(typeof(MyScriptCompiler), "GetIngameScript")]
    public static class GetIngameScriptPatch
    {

        // Add namespace for plugin

        [HarmonyPrefix]
        public static void MyPreFix(MyScriptCompiler __instance) { 

            FieldInfo fieldinfo2 = typeof(MyScriptCompiler).GetField("m_implicitScriptNamespaces", BindingFlags.NonPublic | BindingFlags.Instance);
            Logger logger = new Logger("app.log");

            if (fieldinfo2 != null)
            {
                var nss = (HashSet<string>)fieldinfo2.GetValue(__instance);

                if (!nss.Contains("InteractWithOutsideAPI"))
                {
                    nss.Add("InteractWithOutsideAPI");
                    logger.Log("Namespace Inject success");
                }

            }

        }
    }



    // In Game Code, AddReferencedAssemblies will be called before AddImplicitIngameNamespacesFromTypes to add some assembly,
    // then I just need to call this before AddImplicitIngameNamespacesFromTypes to add my assembly 
    [HarmonyPatch(typeof(MyScriptCompiler), "Compile")]
    public static class CompilePatch
    {
        [HarmonyPrefix]
        public static void MyPrefix(MyScriptCompiler __instance)//ref List<MetadataReference> ___m_metadataReferences
        {
            Logger logger = new Logger("app.log");

            // Add assembly and whitelist for plugin

            FieldInfo fieldinfo = typeof(MyScriptCompiler).GetField("m_metadataReferences", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fieldinfo != null)
            {
                List<MetadataReference> metadataref = (List<MetadataReference>)fieldinfo.GetValue(__instance);

                if(metadataref.Count > 0 && !metadataref.Last().Display.EndsWith("InteractWithOutside.dll")) {
                    metadataref.Add(MetadataReference.CreateFromFile(typeof(CompilePatch).Assembly.Location));

                    IMyWhitelistBatch myWhitelistBatch = MyVRage.Platform.Scripting.OpenWhitelistBatch();
                    myWhitelistBatch.AllowTypes(MyWhitelistTarget.Ingame, typeof(InteractService));


                    if (myWhitelistBatch != null)
                    {
                        myWhitelistBatch.Dispose();
                    }
                    logger.Log("Inject success with " + typeof(CompilePatch).Assembly.Location);
                }
                
            }


            

        }

    }




    // ReSharper disable once UnusedType.Global
    public class Plugin : IPlugin, ICommonPlugin
    {
        public const string Name = "InteractWithOutside";
        public static Plugin Instance { get; private set; }

        public long Tick { get; private set; }
        private static bool failed;

        public IPluginLogger Log => Logger;
        private static readonly IPluginLogger Logger = new PluginLogger(Name);

        public IPluginConfig Config => config?.Data;
        private PersistentConfig<PluginConfig> config;
        private static readonly string ConfigFileName = $"{Name}.cfg";

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Init(object gameInstance)
        {
#if DEBUG
            // Allow the debugger some time to connect once the plugin assembly is loaded
            Thread.Sleep(100);
#endif

            Instance = this;

            Log.Info("Loading");

            var configPath = Path.Combine(MyFileSystem.UserDataPath, ConfigFileName);

            Log.Info(MyFileSystem.UserDataPath);

            config = PersistentConfig<PluginConfig>.Load(Log, configPath);

            var gameVersion = MyFinalBuildConstants.APP_VERSION_STRING.ToString();
            Common.SetPlugin(this, gameVersion, MyFileSystem.UserDataPath);

            if (!PatchHelpers.HarmonyPatchAll(Log, new Harmony(Name)))
            {
                failed = true;
                return;
            }

            Log.Debug("Successfully loaded");

            Logger logger = new Logger("appdomain.log");

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += (sender, args) =>
            {
                //string assemblyName = new AssemblyName(args.Name).Name + ".dll";
                //string assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyName);
                return Assembly.LoadFrom(typeof(Plugin).Assembly.Location);
            };

            logger.Log(currentDomain.FriendlyName);

            var ins = InteractService.Instance;

        }

        public void Dispose()
        {
            try
            {
                // TODO: Save state and close resources here, called when the game exists (not guaranteed!)
                // IMPORTANT: Do NOT call harmony.UnpatchAll() here! It may break other plugins.
            }
            catch (Exception ex)
            {
                Log.Critical(ex, "Dispose failed");
            }

            Instance = null;
        }

        public void Update()
        {
            if (failed)
                return;

            try
            {
                CustomUpdate();
                Tick++;
            }
            catch (Exception ex)
            {
                Log.Critical(ex, "Update failed");
                failed = true;
            }
        }

        private void CustomUpdate()
        {
            // TODO: Put your update code here. It is called on every simulation frame!
            PatchHelpers.PatchUpdates();
        }

        // ReSharper disable once UnusedMember.Global
        public void OpenConfigDialog()
        {
            MyGuiSandbox.AddScreen(new PluginConfigDialog());
        }
    }
}