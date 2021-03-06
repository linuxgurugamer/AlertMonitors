﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP;
using UnityEngine;
using System.IO;
using System.Reflection;
using KSP.UI.Screens;

using ClickThroughFix;
using ToolbarControl_NS;
using Vectrosity;
using System.Reflection.Emit;
using KSP_Log;

namespace ResourceMonitors
{

    [KSPAddon(KSPAddon.Startup.FlightAndKSC, false)]
    partial class ResourceAlertWindow : MonoBehaviour
    {
        internal static ResourceAlertWindow fetch;

        int resourceIndex = 0;

        string resourcePercentage = "20";

        internal AlertSoundPlayer soundplayer = null;
        ToolbarControl toolbarControl = null;

        internal static Rect windowPosition = new Rect(Screen.width / 2 - Main.WIDTH / 2, Screen.height / 2 - Main.HEIGHT / 2, Main.WIDTH, Main.HEIGHT);
        internal static Rect soundWindowPosition = new Rect(Screen.width / 2 - Main.SEL_WIN_WIDTH / 2, Screen.height / 2 - Main.HEIGHT / 2, Main.SEL_WIN_WIDTH, Main.HEIGHT);

        internal static Rect resourceWindowPosition = new Rect(Screen.width / 2 - Main.SEL_WIN_WIDTH / 2, Screen.height / 2 - Main.HEIGHT / 2, Main.SEL_WIN_WIDTH, Main.HEIGHT);



        private bool visible = false; //Inbuilt "visible" boolean, in case I need it for something else.


        static string[] dirEntries;
        static List<string> soundEntriesList = null;

        internal const string MODID = "ResourceMonitors_NS";
        internal const string MODNAME = "Resource Monitors";

        void Start()
        {
            fetch = this;
            if (toolbarControl == null)
            {
                toolbarControl = gameObject.AddComponent<ToolbarControl>();
                toolbarControl.AddToAllToolbars(GuiOn, GuiOff,
                    ApplicationLauncher.AppScenes.SPACECENTER | ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
                    MODID,
                    "dangerAlertButton",
                    "ResourceMonitors/PluginData/Icons/icon_38",
                    "ResourceMonitors/PluginData/Icons/icon_24",
                    MODNAME
                );
            }
            if (soundplayer == null)
            {
                soundplayer = new AlertSoundPlayer();
                soundplayer.Initialize("selection");
            }
            GameEvents.onVesselChange.Add(onVesselChange);
            GameEvents.OnGameSettingsApplied.Add(OnGameSettingsApplied);

            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);

            GameEvents.onShowUI.Add(ShowUI);
            GameEvents.onHideUI.Add(HideUI);
        }
        void onVesselChange(Vessel to)
        {
            Main.Log.Info("GameEvent.onVesselChange: " + to.vesselName);
            GetResourceList(to);
            Get_AlertMonitorsModule(to);
        }

        void OnGameSettingsApplied()
        {
            Main.Log.Info("GameEvent.OnGameSettingsApplied");
            if (HighLogic.CurrentGame.Parameters.CustomParams<RM_3>().resetCommon)
            {
                Scenario_Module.ResetDefaultRMD();
                HighLogic.CurrentGame.Parameters.CustomParams<RM_3>().resetCommon = false;
                ScreenMessages.PostScreenMessage("Common Resource Monitors have been reset to the default settings", 15);
            }
        }


        internal static List<ResourceMonitorDef> workingRMD;



        public void GuiOn()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                workingRMD = null;
                if (FlightGlobals.ActiveVessel != null)
                    workingRMD = FlightGlobals.ActiveVessel.GetComponent<AlertMonitors_Module>().rmdList;
                else
                    workingRMD = null;
                Get_AlertMonitorsModule();
            }
            else
            {
                workingRMD = Scenario_Module.defaultRMD;
            }
            GetResourceList();

            if (soundEntriesList == null)
            {
                dirEntries = Directory.GetFiles("GameData/" + Main.SOUND_DIR, "*.wav");
                soundEntriesList = new List<string>();
                for (int x = 0; x < dirEntries.Count(); x++)
                {
                    string s = dirEntries[x].Substring(0, dirEntries[x].LastIndexOf('.'));
                    s = s.Substring(s.LastIndexOf('/') + 1);
                    soundEntriesList.Add(s);
                }
            }
            visible = true;
        }

        public void GuiOff()
        {
            visible = false;
            toolbarControl.SetFalse(false);
        }


        public List<string> resourceList = new List<string>();

        public void Get_AlertMonitorsModule(Vessel to = null)
        {
            if (to == null)
                to = FlightGlobals.ActiveVessel;
            workingRMD = null;
            if (to != null)
                workingRMD = to.GetComponent<AlertMonitors_Module>().rmdList;
            else
                workingRMD = null;
        }

        public void GetResourceList(Vessel to = null)
        {
            resourceList.Clear();
            Main.Log.Info("GetResourceList");
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (to == null)
                    to = FlightGlobals.ActiveVessel;
                for (int i = 0; i < to.Parts.Count; i++)
                {
                    Part p = to.Parts[i];
                    Main.Log.Info("Part: " + p.partInfo.title);
                    for (int j = 0; j < p.Resources.Count; j++)
                    {
                        PartResource r = p.Resources[j];
                        Main.Log.Info("Part: " + p.partInfo.title + ", resource: " + r.info.name);
                        if (!resourceList.Contains(r.info.name))
                            resourceList.Add(r.info.name);
                    }
                }
            }
            else
            {
                // Space center, get list of all resources in game ???
                foreach (var r in PartResourceLibrary.Instance.resourceDefinitions)
                {
                    if (!Main.common || Main.commonResources.Contains(r.name))
                        resourceList.Add(r.name);
                }
            }
            Main.Log.Info("GetResourceList, total resources found: " + resourceList.Count);
        }

        void SaveSettings()
        {
        }
        public static bool gamePaused;

        private void onGamePause()
        {
            gamePaused = true;
        }
        private void onGameUnpause()
        {
            gamePaused = false;
        }

        float previewWidth = 0;
        private void OnGUI()
        {
            if (hideUI || (gamePaused && HighLogic.CurrentGame.Parameters.CustomParams<RM_2>().hideWhenPaused))
                return;

            if (!HighLogic.CurrentGame.Parameters.CustomParams<RM_2>().altSkin)
            {
                GUI.skin = HighLogic.Skin;
            }
            if (!Main.skinInitialized)
            {
                Main.InitStyles();
            }
            if (visible)
            {
                if (HighLogic.CurrentGame.Parameters.CustomParams<RM_2>().compact)
                {
                    btnStyle = Main.btnCompactStyle;
                    toggleStyle = Main.toggleCompactStyle;
                    redBtnStyle = Main.redButtonCompactStyle;
                    textFieldStyle = Main.textFieldCompactStyle;
                    horSliderStyle = Main.horSliderCompactStyle;
                    thumbStyle = Main.thumbCompactStyle;
                    labelStyle = Main.labelCompactStyle;
                }
                else
                {
                    btnStyle = GUI.skin.button;
                    toggleStyle = GUI.skin.toggle;
                    redBtnStyle = Main.redButtonStyle;
                    textFieldStyle = Main.textFieldStyle;
                    horSliderStyle = Main.horSliderStyle;
                    thumbStyle = Main.thumbStyle;
                    labelStyle = Main.labelStyle;
                }

                windowPosition = ClickThruBlocker.GUILayoutWindow(1987432520, windowPosition, ShowResourceGUIWindow, HighLogic.LoadedSceneIsFlight ? "Vessel Resource Monitors" : "Common Resource Monitors");
                //windowPosition.width = Main.WIDTH + 40;
            }
            if (visible && soundSelectionWindow)
            {
                soundWindowPosition = ClickThruBlocker.GUILayoutWindow(19345098, soundWindowPosition, SoundSelectionWindow, "Sound Selection");
            }
            if (visible && resourceSelectionWindow)
            {
                resourceWindowPosition = ClickThruBlocker.GUILayoutWindow(12345038, resourceWindowPosition, ResourceSelectionWindow, "Resource Selection");
            }
        }

        bool soundSelectionWindow = false;
        bool resourceSelectionWindow = false;
        Vector2 soundFileSelScrollVector;
        Vector2 resourceSelScrollVector;
        string lastSelectedSoundFile = "";
        string lastSelectedResource = "";

        Vector2 scrollPos;
        ResourceMonitorDef resourceAlert;
        GUIStyle btnStyle = null;
        GUIStyle toggleStyle = null;
        GUIStyle redBtnStyle = null;
        GUIStyle textFieldStyle = null;
        GUIStyle horSliderStyle = null;
        GUIStyle thumbStyle = null;
        GUIStyle labelStyle = null;

        ResourceMonitorDef toDel = null;

        int previewSound = 0;
        float oldWidth = 0;
        void ShowResourceGUIWindow(int windowId)
        {
            //Main.WIDTH = 583;
            Main.WIDTH = 613;

            previewWidth = 70;

            if (HighLogic.CurrentGame.Parameters.CustomParams<RM_2>().altSkin)
                Main.WIDTH -= 10;
            if (HighLogic.CurrentGame.Parameters.CustomParams<RM_2>().compact)
                Main.WIDTH -= 10;

            int resourceWidth = 120;
            if (HighLogic.CurrentGame.Parameters.CustomParams<RM_2>().useIconsOnly)
            {
                resourceWidth = 38;
                Main.WIDTH = Main.WIDTH - 120 + 38; // (120 - 38) ;
            }
            if (HighLogic.CurrentGame.Parameters.CustomParams<RM_2>().useIconsAndText)
            {
                Main.WIDTH += 40;
                resourceWidth += 40;
            }
            // Following to avoid flickering when moving the window with certain options specified
            if (Main.WIDTH + 40 != oldWidth)
            {
                oldWidth = Main.WIDTH + 40;
                ResourceAlertWindow.windowPosition.width = oldWidth;
            }
            toDel = null;
            Main.textFieldStyle.normal.textColor = Color.green;
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (HaystackWrapper.HaystackAvailable && HighLogic.CurrentGame.Parameters.CustomParams<RM_1>().useHaystackIfAvailable)
                {
                    GUILayout.BeginHorizontal(); // GUILayout.Width(Main.WIDTH));
                                                 // GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Haystack", btnStyle, GUILayout.Width(120)))
                    {
                        HaystackWrapper.ButtonClick();
                    }
                    GUILayout.FlexibleSpace();
                    if (HaystackWrapper.IsVisible)
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Get Vessel From Haystack", btnStyle, GUILayout.Width(240)))
                        {
                            Main.Log.Info("GetVesselFromHaystack");
                            if (HaystackWrapper.SelectedVessel != null)
                            {
                                Main.Log.Info("Vessel name: " + HaystackWrapper.SelectedVessel.vesselName);
                                JumpAndBackup.JumpToVessel(HaystackWrapper.SelectedVessel);
                            }
                        }
                    }
                    //GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.BeginHorizontal(); // GUILayout.Width(Main.WIDTH));
                GUILayout.FlexibleSpace();
                GUILayout.Label("Common Resource Monitor Definition");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            var b = GUILayout.Toggle(Main.common, "Only allow common resources for new monitors", toggleStyle);
            if (b != Main.common)
            {
                Main.common = b;
                GetResourceList();
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); // GUILayout.Width(Main.WIDTH));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add Resource Monitor", btnStyle))
            {
                workingRMD.Add(new ResourceMonitorDef("ElectricCharge", "Alarm1", 5f, 1));

                resourceIndex = workingRMD.Count - 1; //sets index to last one
            }
            if (workingRMD.Count == 0 && HighLogic.LoadedSceneIsFlight)
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Apply Common Resource Monitor Settings", btnStyle))
                {
                    AddCommonResourceMonitors();
                    resourceIndex = workingRMD.Count - 1; //sets index to last one
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(25);
            if (HighLogic.CurrentGame.Parameters.CustomParams<RM_2>().useIconsOnly)
                GUILayout.Label("Res", GUILayout.Width(resourceWidth));
            else
                GUILayout.Label("Resource", GUILayout.Width(resourceWidth));
            GUILayout.Space(25);
            GUILayout.Label("Alarm Sound", GUILayout.Width(120));


            //GUILayout.Space(30);
            //GUILayout.Space(15);
            //GUILayout.Space(30);

            GUILayout.Space(75);
            GUILayout.Label("Amt or % ");
            //GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();




            GUILayout.BeginHorizontal();
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Width(Main.WIDTH + 20));

            for (int i = 0; i < workingRMD.Count; i++)
            {
                GUILayout.BeginHorizontal(GUILayout.Width(Main.WIDTH + 20));
                resourceAlert = workingRMD[i];
                if (resourceAlert.Enabled)
                {
                    Main.buttonStyle.normal.textColor = Color.green;
                    Main.toggleStyle.normal.textColor = Color.green;
                    Main.textFieldStyle.normal.textColor = Color.green;
                }
                else
                {
                    Main.buttonStyle.normal.textColor = Color.red;
                    Main.toggleStyle.normal.textColor = Color.red;
                    Main.textFieldStyle.normal.textColor = Color.red;

                }
                int curResource = -1;


                // ****************************************************************************
                //                                                                            *
                GUIContent btn = null;

                if (!HighLogic.CurrentGame.Parameters.CustomParams<RM_2>().useTextOnly && Main.icons.ContainsKey(resourceAlert.resname))
                {
                    if (HighLogic.CurrentGame.Parameters.CustomParams<RM_2>().useIconsOnly)
                        btn = new GUIContent(Main.icons[resourceAlert.resname]);
                    if (HighLogic.CurrentGame.Parameters.CustomParams<RM_2>().useIconsAndText)
                        btn = new GUIContent(PartResourceLibrary.Instance.resourceDefinitions[resourceAlert.resname].displayName,
                            Main.icons[resourceAlert.resname]);
                }
                else
                {
                    btn = new GUIContent(PartResourceLibrary.Instance.resourceDefinitions[resourceAlert.resname].displayName);
                }


                if (GUILayout.Button(btn, btnStyle, GUILayout.Width(resourceWidth)))
                {
                    resourceSelectionWindow = !resourceSelectionWindow;
                    if (resourceSelectionWindow)
                    {
                        lastSelectedResource = resourceAlert.resname;
                    }
                }

                GUILayout.Space(10);
                //                                                                            *
                // ****************************************************************************

                // ****************************************************************************
                //                                                                            *
                if (GUILayout.Button(resourceAlert.alarm, btnStyle, GUILayout.Width(120)))
                {
                    soundSelectionWindow = !soundSelectionWindow;
                    if (soundSelectionWindow)
                    {
                        lastSelectedSoundFile = resourceAlert.alarm;
                    }
                }

                GUILayout.Space(10);
                //                                                                            *
                // ****************************************************************************
                if (GUILayout.Button(resourceAlert.monitorHighValue ? ">" : "<", GUILayout.Width(20)))
                {
                    resourceAlert.monitorHighValue = !resourceAlert.monitorHighValue;
                }
                GUILayout.Space(10);
                var newMonitorByPercentage = GUILayout.Toggle(resourceAlert.monitorByPercentage, "", toggleStyle, GUILayout.Width(30));
                //GUILayout.Label("%", labelStyle, GUILayout.Width(20));
                if (newMonitorByPercentage)
                {
                    if (HighLogic.LoadedSceneIsFlight && !resourceAlert.monitorByPercentage)
                    {
                        //if (resourceAlert.percentage == 0 && resourceAlert.minAmt > 0)
                        {

                            var resource = PartResourceLibrary.Instance.GetDefinition(resourceAlert.resname);
                            FlightGlobals.ActiveVessel.rootPart.GetConnectedResourceTotals(resource.id, out double amount, out double maxAmount, true);
                            resourceAlert.percentage = (float)(100f * resourceAlert.minAmt / maxAmount);
                        }
                        resourceAlert.minAmt = 0;
                    }
                    float f = Math.Max(1f, Math.Min(resourceAlert.percentage, 99));

                    f = GUILayout.HorizontalSlider(f, 1f, 99f, horSliderStyle, thumbStyle, GUILayout.Width(100));
                    resourceAlert.percentage = (int)f;
                    string p = f.ToString("F0") + "%";
                    GUILayout.Label(p, GUILayout.Width(30));
                }
                else
                {
                    var labelStyleAmt = new GUIStyle(labelStyle);
                    labelStyleAmt.alignment = TextAnchor.MiddleRight;
                    GUILayout.Label("Amt:", labelStyleAmt, GUILayout.Width(40));
                    if (HighLogic.LoadedSceneIsFlight && resourceAlert.monitorByPercentage)
                    {
                        if (!newMonitorByPercentage && resourceAlert.percentage > 0 && resourceAlert.minAmt == 0)
                        {

                            var resource = PartResourceLibrary.Instance.GetDefinition(resourceAlert.resname);
                            FlightGlobals.ActiveVessel.rootPart.GetConnectedResourceTotals(resource.id, out double amount, out double maxAmount, true);
                            resourceAlert.minAmt = resourceAlert.percentage / 100f * maxAmount;
                        }
                        resourceAlert.percentage = 0;
                    }
                    string f = resourceAlert.minAmt.ToString("F1");
                    f = GUILayout.TextField(f, textFieldStyle, GUILayout.Width(90));
                    if (double.TryParse(f, out double d))
                    {
                        resourceAlert.minAmt = Math.Max(0.1, d);
                    }
                }
                resourceAlert.monitorByPercentage = newMonitorByPercentage;
                resourceAlert.Enabled = GUILayout.Toggle(resourceAlert.Enabled, "", toggleStyle, GUILayout.Width(30));
                GUILayout.Space(5);
                //if (HighLogic.CurrentGame.Parameters.CustomParams<AlertMonitor>().altSkin)
                //    GUILayout.Space(10);
                if (GUILayout.Button("X", redBtnStyle, GUILayout.Width(23)))
                    toDel = resourceAlert;
                GUILayout.Space(5);
                if (previewSound != curResource || !soundplayer.SoundPlaying())
                {

                    if (GUILayout.Button("Preview", btnStyle, GUILayout.Width(previewWidth)))
                    {
                        previewSound = curResource;
                        Main.Log.Info("Preview: " + Main.SOUND_DIR + resourceAlert.alarm);
                        soundplayer.LoadNewSound(Main.SOUND_DIR + resourceAlert.alarm, HighLogic.CurrentGame.Parameters.CustomParams<RM_1>().resourceAlertRepetition);
                        soundplayer.SetVolume(HighLogic.CurrentGame.Parameters.CustomParams<RM_2>().masterVolume);

                        soundplayer.PlaySound(true); //Plays sound
                    }
                }
                else
                {
                    if (GUILayout.Button("Stop", GUILayout.Width(previewWidth)))
                        soundplayer.StopSound();
                }
                GUILayout.EndHorizontal();

            }
            GUILayout.EndScrollView();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (toDel != null)
                workingRMD.Remove(toDel);
            GUILayout.BeginHorizontal(); // GUILayout.Width(Main.WIDTH));

            if (HighLogic.LoadedSceneIsFlight)
            {
                if (GUILayout.Button("Close"))
                {
                    SaveSettings();
                    GuiOff();
                    soundSelectionWindow = false;
                }
            }
            else
            {
                if (GUILayout.Button("Save"))
                {
                    SaveSettings();
                    GuiOff();
                    soundSelectionWindow = false;
                }
                if (GUILayout.Button("Cancel"))
                {
                    GuiOff();
                    soundSelectionWindow = false;
                }

            }
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        internal static void AddCommonResourceMonitors()
        {
            if (workingRMD == null)
                workingRMD = FlightGlobals.ActiveVessel.GetComponent<AlertMonitors_Module>().rmdList;

            Main.Log.Info("AddCommonResourceMonitors, ResourceAlertWindow.fetch != null: " + (ResourceAlertWindow.fetch != null).ToString());
            if (ResourceAlertWindow.fetch != null)
            {
                Main.Log.Info("AddCommonResourceMonitors, defaultRMD.Count: " + Scenario_Module.defaultRMD.Count + ", ResourceAlertWindow.fetch.resourceList: " + ResourceAlertWindow.fetch.resourceList.Count + ", workingRMD.Count: " + workingRMD.Count);

                
                foreach (var r in Scenario_Module.defaultRMD)
                {
                    Main.Log.Info("r.resname: " + r.resname);
                    if (ResourceAlertWindow.fetch.resourceList.Contains(r.resname))
                    {
                        Main.Log.Info("Adding " + r.resname + " to workingRMD");
                        //if (workingRMD.Where(w => w.resname == r.resname).Count() == 0)
                            workingRMD.Add(new ResourceMonitorDef(r));
                    }
                }
            }
        }

        bool ResourceValueCheck()
        {
            bool changed = false;

            if (resourceIndex < 0)
            {
                resourceIndex = 0;
                changed = true;
            }
            try
            {
                if (Int32.Parse(resourcePercentage) < 0 || Int32.Parse(resourcePercentage) > 100)
                {
                    resourcePercentage = "50";
                    changed = true;
                }
            }
            catch
            {
                resourcePercentage = "50";
                changed = true;
            }

            return changed;
        }


        public Rect GetPosition()
        {
            return windowPosition;
        }

        bool hideUI;
        private void ShowUI()
        {
            hideUI = false;
        }
        void HideUI()
        {
            hideUI = true;
        }



        void OnDestroy()
        {
            Main.Log.Info("ResourceAlertWindow is being destroyed");
            toolbarControl.OnDestroy();
            Destroy(toolbarControl);
            GameEvents.onVesselChange.Remove(onVesselChange);
            GameEvents.OnGameSettingsApplied.Remove(OnGameSettingsApplied);
            GameEvents.onGamePause.Remove(onGamePause);
            GameEvents.onGameUnpause.Remove(onGameUnpause);

            GameEvents.onShowUI.Remove(ShowUI);
            GameEvents.onHideUI.Remove(HideUI);

            SaveSettings();
        }
    }
}
