using BepInEx;
using HarmonyLib;
using System;
using System.IO;
using UnityEngine;

namespace TakeTheDamnVideo
{
    [BepInPlugin("com.narezany.takethedamnvideo", "Take The Damn Video", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static object ActiveEncoder = null;

        private void Awake()
        {
            try
            {
                var harmony = new Harmony("com.narezany.takethedamnvideo");
                harmony.PatchAll(typeof(Plugin).Assembly);

                // Spawn our standalone frame capture manager
                var go = new GameObject("TakeTheDamnVideo_Manager");
                GameObject.DontDestroyOnLoad(go);
                go.AddComponent<CustomVideoRecorder>();

                Logger.LogInfo("Take The Damn Video loaded successfully! Custom AVI/MJPEG encoder, standalone capturer, custom CopyToGallery and fallback LckMonoBehaviourMediator initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error initializing Take The Damn Video: {ex}");
            }
        }
    }

    public class CustomVideoRecorder : MonoBehaviour
    {
        private float nextFrameTime = 0.0f;
        private float frameDuration = 0.0f;
        private float elapsed = 0.0f;

        void Update()
        {
            if (AviWriter.IsActive && Plugin.ActiveEncoder != null)
            {
                try
                {
                    if (frameDuration <= 0.0f)
                    {
                        int fps = AviWriter.fps;
                        if (fps <= 0) fps = 30; // Fallback
                        frameDuration = 1.0f / fps;
                        nextFrameTime = 0.0f;
                        elapsed = 0.0f;
                    }

                    elapsed += Time.unscaledDeltaTime;

                    if (elapsed >= nextFrameTime)
                    {
                        var encoder = Plugin.ActiveEncoder;
                        var textureProvider = AccessTools.Field(encoder.GetType(), "_videoTextureProvider").GetValue(encoder);
                        if (textureProvider != null)
                        {
                            var rt = (RenderTexture)AccessTools.Property(textureProvider.GetType(), "CameraTrackTexture").GetValue(textureProvider);
                            if (rt != null)
                            {
                                AviWriter.WriteFrame(rt);
                                nextFrameTime += frameDuration;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[TakeTheDamnVideo] CustomVideoRecorder Update Error: {ex}");
                }
            }
            else
            {
                frameDuration = 0.0f;
            }
        }
    }

    [HarmonyPatch]
    public static class LckMonoBehaviourMediatorInstancePatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            var mediatorType = AccessTools.TypeByName("Liv.Lck.LckMonoBehaviourMediator");
            return AccessTools.Method(mediatorType, "get_Instance");
        }

        [HarmonyPrefix]
        public static bool Prefix(ref object __result)
        {
            try
            {
                var mediatorType = AccessTools.TypeByName("Liv.Lck.LckMonoBehaviourMediator");
                var instanceField = AccessTools.Field(mediatorType, "_instance");
                var currentInstance = instanceField.GetValue(null);

                // Use Unity's custom null comparison to detect destroyed MonoBehaviours
                var unityInstance = currentInstance as UnityEngine.Object;

                if (unityInstance == null)
                {
                    var go = new GameObject("TakeTheDamnVideo_LckMonoBehaviourMediator");
                    GameObject.DontDestroyOnLoad(go);
                    currentInstance = go.AddComponent(mediatorType);
                    instanceField.SetValue(null, currentInstance);
                    UnityEngine.Debug.Log("[TakeTheDamnVideo] Created fallback LckMonoBehaviourMediator instance to prevent NullReferenceException.");
                }
                __result = currentInstance;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[TakeTheDamnVideo] LckMonoBehaviourMediatorInstancePatch error: {ex}");
            }
            return false;
        }
    }

    [HarmonyPatch]
    public static class GetIsActivePatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            var encoderType = AccessTools.TypeByName("Liv.Lck.Encoding.LckEncoder");
            return AccessTools.Method(encoderType, "IsActive");
        }

        [HarmonyPrefix]
        public static bool Prefix(ref bool __result)
        {
            __result = AviWriter.IsActive;
            return false;
        }
    }

    [HarmonyPatch]
    public static class StartRecordingProcessPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Liv.Lck.Recorder.LckRecorder:StartRecordingProcess");
        }

        [HarmonyPostfix]
        public static void Postfix(object __instance)
        {
            try
            {
                var config = AccessTools.Field(__instance.GetType(), "_muxerConfig").GetValue(__instance);
                if (config != null)
                {
                    var outputPath = (string)AccessTools.Field(config.GetType(), "outputPath").GetValue(config);
                    var width = (uint)AccessTools.Field(config.GetType(), "width").GetValue(config);
                    var height = (uint)AccessTools.Field(config.GetType(), "height").GetValue(config);
                    var framerate = (uint)AccessTools.Field(config.GetType(), "framerate").GetValue(config);

                    UnityEngine.Debug.Log($"[TakeTheDamnVideo] StartRecordingProcess. Output: {outputPath}, Dimensions: {width}x{height} @ {framerate}fps");
                    AviWriter.Start(outputPath, (int)width, (int)height, (int)framerate);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[TakeTheDamnVideo] StartRecordingProcess Postfix error: {ex}");
            }
        }
    }

    [HarmonyPatch]
    public static class StopRecordingProcessPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Liv.Lck.Recorder.LckRecorder:StopRecordingProcess");
        }

        [HarmonyPrefix]
        public static void Prefix(object __instance)
        {
            try
            {
                UnityEngine.Debug.Log("[TakeTheDamnVideo] StopRecordingProcess. Closing AVI Writer.");
                AviWriter.Close();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[TakeTheDamnVideo] StopRecordingProcess Prefix error: {ex}");
            }
        }
    }

    [HarmonyPatch]
    public static class EncodeFramePatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Liv.Lck.Encoding.LckEncoder:EncodeFrame");
        }

        [HarmonyPrefix]
        public static bool Prefix(object __instance, float videoTimeSeconds, object audioData, bool encodeVideo, ref bool __result)
        {
            // The encoding frames are now captured by CustomVideoRecorder at Update rate to bypass audio/native capture issues.
            __result = true;
            return false;
        }
    }

    [HarmonyPatch]
    public static class CopyToGalleryPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Liv.Lck.Utilities.FileUtility");
            return AccessTools.Method(type, "CopyToGallery");
        }

        [HarmonyPrefix]
        public static bool Prefix(string sourceFilePath, string albumName, Action<bool, string> callback, ref object __result)
        {
            string destPath = "";
            try
            {
                UnityEngine.Debug.Log($"[TakeTheDamnVideo] CopyToGallery Intercepted! Source: {sourceFilePath}");

                // Determine target folder based on extension
                string ext = Path.GetExtension(sourceFilePath).ToLower();
                string targetFolder = "";
                if (ext == ".mp4" || ext == ".avi")
                {
                    targetFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Damn videos");
                }
                else
                {
                    targetFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Damn photos");
                }

                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                destPath = Path.Combine(targetFolder, Path.GetFileName(sourceFilePath));

                // Copy the file
                if (File.Exists(sourceFilePath))
                {
                    File.Copy(sourceFilePath, destPath, true);
                    UnityEngine.Debug.Log($"[TakeTheDamnVideo] Successfully copied {sourceFilePath} to {destPath}");
                    
                    // Delete the temp file to be clean
                    try
                    {
                        File.Delete(sourceFilePath);
                    }
                    catch {}
                }
                else
                {
                    UnityEngine.Debug.LogError($"[TakeTheDamnVideo] Source file not found: {sourceFilePath}");
                }

                // Invoke callback
                if (callback != null)
                {
                    try
                    {
                        callback.Invoke(true, destPath);
                    }
                    catch (Exception callbackEx)
                    {
                        UnityEngine.Debug.LogError($"[TakeTheDamnVideo] Error in CopyToGallery callback: {callbackEx}");
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[TakeTheDamnVideo] CopyToGallery Patch error: {ex}");
            }

            // Return a completed task containing the ValueTuple<bool, string> directly and safely
            __result = System.Threading.Tasks.Task.FromResult(new ValueTuple<bool, string>(true, destPath));
            return false; // Skip the original buggy CopyToGallery method
        }
    }

    // --- High-Level C# Muxer and Encoder Stubs ---
    // This bypasses the native C++ library lck_rs.dll completely by intercepting the high-level C# wrappers

    [HarmonyPatch]
    public static class CreateNativeMuxerPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Liv.Lck.Recorder.LckNativeRecordingService:CreateNativeMuxer");
        }

        [HarmonyPrefix]
        public static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }

    [HarmonyPatch]
    public static class StartNativeMuxerPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Liv.Lck.Recorder.LckNativeRecordingService:StartNativeMuxer");
        }

        [HarmonyPrefix]
        public static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }

    [HarmonyPatch]
    public static class StopNativeMuxerPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Liv.Lck.Recorder.LckNativeRecordingService:StopNativeMuxer");
        }

        [HarmonyPrefix]
        public static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }

    [HarmonyPatch]
    public static class DestroyNativeMuxerPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Liv.Lck.Recorder.LckNativeRecordingService:DestroyNativeMuxer");
        }

        [HarmonyPrefix]
        public static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch]
    public static class SetNativeMuxerLogLevelPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Liv.Lck.Recorder.LckNativeRecordingService:SetNativeMuxerLogLevel");
        }

        [HarmonyPrefix]
        public static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch]
    public static class GetMuxPacketCallbackPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Liv.Lck.Recorder.LckNativeRecordingService:GetMuxPacketCallback");
        }

        [HarmonyPrefix]
        public static bool Prefix(ref object __result)
        {
            try
            {
                var callbackType = AccessTools.TypeByName("Liv.Lck.Encoding.LckEncodedPacketCallback");
                __result = Activator.CreateInstance(callbackType);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[TakeTheDamnVideo] GetMuxPacketCallback Stub error: {ex}");
            }
            return false;
        }
    }

    [HarmonyPatch]
    public static class StartEncoderInternalPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Liv.Lck.Encoding.LckEncoder:StartEncoderInternal");
        }

        [HarmonyPrefix]
        public static bool Prefix(object __instance, ref object __result)
        {
            try
            {
                Plugin.ActiveEncoder = __instance; // Save active encoder instance
                UnityEngine.Debug.Log($"[TakeTheDamnVideo] StartEncoderInternal active. Saved encoder instance: {__instance}");

                // Set the private field _isActive to true to signal we are encoding
                AccessTools.Field(__instance.GetType(), "_isActive").SetValue(__instance, true);

                var lckResultType = AccessTools.TypeByName("Liv.Lck.LckResult");
                var newSuccessMethod = AccessTools.Method(lckResultType, "NewSuccess");
                __result = newSuccessMethod.Invoke(null, null);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[TakeTheDamnVideo] StartEncoderInternal Stub error: {ex}");
            }
            return false;
        }
    }

    [HarmonyPatch]
    public static class ReleaseEncoderAsyncPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            var encoderType = AccessTools.TypeByName("Liv.Lck.Encoding.LckEncoder");
            return AccessTools.Method(encoderType, "ReleaseEncoderAsync");
        }

        [HarmonyPrefix]
        public static bool Prefix(object __instance, ref object __result)
        {
            try
            {
                UnityEngine.Debug.Log("[TakeTheDamnVideo] ReleaseEncoderAsync Intercepted. Forcing successful release.");
                
                // Call StopEncoderInternal to clean up encoder state
                AccessTools.Method(__instance.GetType(), "StopEncoderInternal").Invoke(__instance, null);
                
                // Return a successful LckResult inside Task
                var lckResultType = AccessTools.TypeByName("Liv.Lck.LckResult");
                var newSuccessMethod = AccessTools.Method(lckResultType, "NewSuccess");
                var successResult = newSuccessMethod.Invoke(null, null);
                
                __result = System.Threading.Tasks.Task.FromResult(successResult);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[TakeTheDamnVideo] ReleaseEncoderAsync Patch error: {ex}");
                
                var lckResultType = AccessTools.TypeByName("Liv.Lck.LckResult");
                var newSuccessMethod = AccessTools.Method(lckResultType, "NewSuccess");
                var successResult = newSuccessMethod.Invoke(null, null);
                __result = System.Threading.Tasks.Task.FromResult(successResult);
            }
            return false;
        }
    }

    [HarmonyPatch]
    public static class StopNativeEncoderPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Liv.Lck.Encoding.LckEncoder:StopNativeEncoder");
        }

        [HarmonyPrefix]
        public static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch]
    public static class StopEncoderInternalPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Liv.Lck.Encoding.LckEncoder:StopEncoderInternal");
        }

        [HarmonyPrefix]
        public static bool Prefix(object __instance)
        {
            try
            {
                Plugin.ActiveEncoder = null; // Clear active encoder

                // Revert _isActive to false when stopping
                AccessTools.Field(__instance.GetType(), "_isActive").SetValue(__instance, false);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[TakeTheDamnVideo] StopEncoderInternal Stub error: {ex}");
            }
            return false;
        }
    }

    [HarmonyPatch]
    public static class SetLogLevelPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Liv.Lck.Encoding.LckEncoder:SetLogLevel");
        }

        [HarmonyPrefix]
        public static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch]
    public static class AddEncodedPacketHandlerPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Liv.Lck.Encoding.LckEncoder:AddEncodedPacketHandler");
        }

        [HarmonyPrefix]
        public static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch]
    public static class AddEncodedPacketHandlersPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Liv.Lck.Encoding.LckEncoder:AddEncodedPacketHandlers");
        }

        [HarmonyPrefix]
        public static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch]
    public static class RemoveEncodedPacketHandlerPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Liv.Lck.Encoding.LckEncoder:RemoveEncodedPacketHandler");
        }

        [HarmonyPrefix]
        public static bool Prefix()
        {
            return false;
        }
    }
}
