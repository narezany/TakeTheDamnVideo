using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace TakeTheDamnVideo
{
    public static class AviWriter
    {
        private static FileStream stream;
        private static BinaryWriter writer;
        private static int width;
        private static int height;
        public static int fps;
        private static int frameCount;
        private static List<long> offsets = new List<long>();
        private static List<int> sizes = new List<int>();

        private static long fileLengthOffset;
        private static long avihFrameCountOffset;
        private static long strhLengthOffset;
        private static long moviListSizeOffset;
        private static long moviListDataStart;

        private static Texture2D tempTexture;

        public static bool IsActive => stream != null;

        public static void Start(string path, int w, int h, int framerate)
        {
            try
            {
                width = w;
                height = h;
                fps = framerate;

                // Ensure the directory exists
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                writer = new BinaryWriter(stream);

                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                fileLengthOffset = stream.Position;
                writer.Write((int)0); // Placeholder for RIFF size
                writer.Write(System.Text.Encoding.ASCII.GetBytes("AVI "));

                // Header List (hdrl)
                writer.Write(System.Text.Encoding.ASCII.GetBytes("LIST"));
                long hdrlSizeOffset = stream.Position;
                writer.Write((int)0); // Placeholder for hdrl list size
                writer.Write(System.Text.Encoding.ASCII.GetBytes("hdrl"));

                // Main AVI Header (avih)
                writer.Write(System.Text.Encoding.ASCII.GetBytes("avih"));
                writer.Write((int)56); // avih size
                writer.Write((int)(1000000 / fps)); // microSecPerFrame
                writer.Write((int)0); // maxBytesPerSec
                writer.Write((int)0); // paddingGranularity
                writer.Write((int)0x10); // flags (AVIF_HASINDEX)
                avihFrameCountOffset = stream.Position;
                writer.Write((int)0); // Placeholder for totalFrames
                writer.Write((int)0); // initialFrames
                writer.Write((int)1); // streams
                writer.Write((int)(1024 * 1024)); // suggestedBufferSize
                writer.Write((int)width);
                writer.Write((int)height);
                for (int i = 0; i < 4; i++) writer.Write((int)0); // reserved

                // Stream List (strl)
                writer.Write(System.Text.Encoding.ASCII.GetBytes("LIST"));
                long strlSizeOffset = stream.Position;
                writer.Write((int)0); // Placeholder for strl list size
                writer.Write(System.Text.Encoding.ASCII.GetBytes("strl"));

                // Stream Header (strh)
                writer.Write(System.Text.Encoding.ASCII.GetBytes("strh"));
                writer.Write((int)56); // strh size
                writer.Write(System.Text.Encoding.ASCII.GetBytes("vids"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("MJPG"));
                writer.Write((int)0); // flags
                writer.Write((short)0); // priority
                writer.Write((short)0); // language
                writer.Write((int)0); // initialFrames
                writer.Write((int)1); // scale
                writer.Write((int)fps); // rate
                writer.Write((int)0); // start
                strhLengthOffset = stream.Position;
                writer.Write((int)0); // Placeholder for length (frames)
                writer.Write((int)(1024 * 1024)); // suggestedBufferSize
                writer.Write((int)(-1)); // quality
                writer.Write((int)0); // sampleSize
                writer.Write((short)0); // rcFrame Left
                writer.Write((short)0); // rcFrame Top
                writer.Write((short)width); // rcFrame Right
                writer.Write((short)height); // rcFrame Bottom

                // Stream Format (strf)
                writer.Write(System.Text.Encoding.ASCII.GetBytes("strf"));
                writer.Write((int)40); // strf size
                writer.Write((int)40); // biSize
                writer.Write((int)width); // biWidth
                writer.Write((int)height); // biHeight
                writer.Write((short)1); // biPlanes
                writer.Write((short)24); // biBitCount
                writer.Write(System.Text.Encoding.ASCII.GetBytes("MJPG")); // biCompression
                writer.Write((int)(width * height * 3)); // biSizeImage
                writer.Write((int)0); // biXPelsPerMeter
                writer.Write((int)0); // biYPelsPerMeter
                writer.Write((int)0); // biClrUsed
                writer.Write((int)0); // biClrImportant

                // Finalize strl size
                long afterStrlOffset = stream.Position;
                stream.Position = strlSizeOffset;
                writer.Write((int)(afterStrlOffset - strlSizeOffset - 4));
                stream.Position = afterStrlOffset;

                // Finalize hdrl size
                long afterHdrlOffset = stream.Position;
                stream.Position = hdrlSizeOffset;
                writer.Write((int)(afterHdrlOffset - hdrlSizeOffset - 4));
                stream.Position = afterHdrlOffset;

                // Movie List (movi)
                writer.Write(System.Text.Encoding.ASCII.GetBytes("LIST"));
                moviListSizeOffset = stream.Position;
                writer.Write((int)0); // Placeholder for movi size
                writer.Write(System.Text.Encoding.ASCII.GetBytes("movi"));
                moviListDataStart = stream.Position;

                frameCount = 0;
                offsets.Clear();
                sizes.Clear();

                UnityEngine.Debug.Log($"[TakeTheDamnVideo] Custom AVI writer successfully initialized at {path}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[TakeTheDamnVideo] Error starting AVI writer: {ex}");
                Close();
            }
        }

        public static void WriteFrame(RenderTexture rt)
        {
            if (stream == null) return;

            try
            {
                if (tempTexture == null || tempTexture.width != rt.width || tempTexture.height != rt.height)
                {
                    if (tempTexture != null) UnityEngine.Object.Destroy(tempTexture);
                    tempTexture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                }

                RenderTexture active = RenderTexture.active;
                RenderTexture.active = rt;
                tempTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tempTexture.Apply();
                RenderTexture.active = active;

                byte[] jpegBytes = tempTexture.EncodeToJPG(80); // 80% quality

                long chunkOffset = stream.Position - moviListDataStart;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("00dc"));
                writer.Write(jpegBytes.Length);
                writer.Write(jpegBytes);

                int pad = jpegBytes.Length % 2;
                if (pad > 0)
                {
                    writer.Write((byte)0);
                }

                offsets.Add(chunkOffset);
                sizes.Add(jpegBytes.Length);
                frameCount++;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[TakeTheDamnVideo] Error writing video frame: {ex}");
            }
        }

        public static void Close()
        {
            if (stream == null) return;

            try
            {
                // Write index (idx1)
                long idx1StartOffset = stream.Position;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("idx1"));
                writer.Write((int)(frameCount * 16));
                for (int i = 0; i < frameCount; i++)
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("00dc"));
                    writer.Write((int)0x10); // AVIIF_KEYFRAME
                    writer.Write((int)offsets[i]);
                    writer.Write((int)sizes[i]);
                }

                long fileLength = stream.Position;

                // Patch RIFF size
                stream.Position = fileLengthOffset;
                writer.Write((int)(fileLength - fileLengthOffset - 4));

                // Patch avih frame count
                stream.Position = avihFrameCountOffset;
                writer.Write((int)(frameCount));

                // Patch strh length
                stream.Position = strhLengthOffset;
                writer.Write((int)(frameCount));

                // Patch movi size
                stream.Position = moviListSizeOffset;
                writer.Write((int)(idx1StartOffset - moviListSizeOffset - 4));

                writer.Flush();
                UnityEngine.Debug.Log($"[TakeTheDamnVideo] Custom AVI writer closed. Saved {frameCount} frames.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[TakeTheDamnVideo] Error closing AVI writer: {ex}");
            }
            finally
            {
                if (writer != null) writer.Close();
                if (stream != null) stream.Close();
                writer = null;
                stream = null;

                if (tempTexture != null)
                {
                    UnityEngine.Object.Destroy(tempTexture);
                    tempTexture = null;
                }
            }
        }
    }
}
