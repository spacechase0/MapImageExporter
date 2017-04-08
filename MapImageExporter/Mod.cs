using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using System;
using System.Collections.Concurrent;
using System.IO;

namespace MapImageExporter
{
    public class Mod : StardewModdingAPI.Mod
    {
        public static Mod instance;
        private ConcurrentQueue<GameLocation> renderQueue = new ConcurrentQueue<GameLocation>();

        public override void Entry(IModHelper helper)
        {
            instance = this;

            Helper.ConsoleCommands.Add("export", "See 'export help'", exportCommand);
            GameEvents.UpdateTick += checkRenderQueue;
        }

        private void exportCommand( string str, string[] args )
        {
            if ( args.Length < 1 )
            {
                Log.error("No command/map_name given.");
                return;
            }

            if ( args[ 0 ] == "all" )
            {
                foreach ( GameLocation loc in Game1.locations )
                {
                    renderQueue.Enqueue(loc);
                }
            }
            else if (args[0] == "list")
            {
                if ( Game1.locations.Count == 0 )
                {
                    Log.info("No maps loaded.");
                    return;
                }

                string maps = Game1.locations[0].name;
                foreach (GameLocation loc in Game1.locations)
                {
                    if (loc == Game1.locations[0])
                        continue;
                    maps += ", " + loc.name;
                }

                Log.info("Maps: " + maps);
            }
            else if (args[0] == "current")
            {
                renderQueue.Enqueue(Game1.currentLocation);
            }
            else if ( args[ 1 ] == "help" )
            {
                Log.info("Commands: ");
                Log.info("\texport all [settings] - Export all locations.");
                Log.info("\texport list - Get a list of available maps.");
                Log.info("\texport current [settings] - Export your current location.");
                Log.info("\texport <map_name> [settings] - Export map_name.");
                Log.info("\texport help - Print this block of text.");
                Log.info("Settings: ");
                Log.info("\tTODO");
            }
            else
            {
                GameLocation loc = Game1.getLocationFromName(args[0]);
                if ( loc == null )
                {
                    Log.error("Bad map name");
                    return;
                }
                renderQueue.Enqueue(loc);
            }
        }

        private void checkRenderQueue( object sender, EventArgs args )
        {
            // Simply doing the export when the command is called can cause issues (not
            // actually rendering, making the game skip a frame, crashing, ...) since
            // the console runs on another thread. Rendering might happen at the same 
            // time as the main game. So instead we're going to render one per frame
            // in the update stage, so things definitely don't interfere.

            GameLocation loc = null;
            if (!renderQueue.TryDequeue(out loc))
            {
                return;
            }
            export(loc);
        }

        private void export( GameLocation loc )
        {
            SpriteBatch b = new SpriteBatch(Game1.graphics.GraphicsDevice);
            GraphicsDevice dev = Game1.graphics.GraphicsDevice;
            var display = Game1.mapDisplayDevice;
            RenderTarget2D output = null;
            Stream stream = null;
            bool begun = false;
            try
            {
                Log.info("Rendering " + loc.name + "...");
                output = new RenderTarget2D(dev, loc.map.DisplayWidth / 4, loc.map.DisplayHeight / 4);

                dev.SetRenderTarget(output);
                dev.Clear(Color.Black);
                {
                    loc.map.LoadTileSheets(display);

                    b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
                    begun = true;
                    display.BeginScene(b);
                    loc.map.GetLayer("Back").Draw(Game1.mapDisplayDevice, new xTile.Dimensions.Rectangle(0, 0, output.Width, output.Height), xTile.Dimensions.Location.Origin, false, 1);
                    loc.map.GetLayer("Buildings").Draw(Game1.mapDisplayDevice, new xTile.Dimensions.Rectangle(0, 0, output.Width, output.Height), xTile.Dimensions.Location.Origin, false, 1);
                    display.EndScene();
                    display.EndScene();
                    b.End();
                    begun = false;

                    b.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
                    begun = true;
                    display.BeginScene(b);
                    loc.map.GetLayer("Front").Draw(Game1.mapDisplayDevice, new xTile.Dimensions.Rectangle(0, 0, output.Width, output.Height), xTile.Dimensions.Location.Origin, false, 1);
                    display.EndScene();
                    b.End();
                    begun = false;

                    if (loc.map.GetLayer("AlwaysFront") != null)
                    {
                        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
                        begun = true;
                        display.BeginScene(b);
                        loc.map.GetLayer("AlwaysFront").Draw(Game1.mapDisplayDevice, new xTile.Dimensions.Rectangle(0, 0, output.Width, output.Height), xTile.Dimensions.Location.Origin, false, 1);
                        display.EndScene();
                        b.End();
                        begun = false;
                    }
                }
                dev.SetRenderTarget(null);

                string name = loc.name;
                if ( loc.uniqueName != null )
                    name = loc.uniqueName;

                string dirPath = Helper.DirectoryPath + "/../../MapExport";
                string imagePath = dirPath + "/" + name + ".png";
                Log.info("Saving " + name + " to " + imagePath + "...");

                if (!Directory.Exists(dirPath))
                    Directory.CreateDirectory(dirPath);

                stream = File.Create(imagePath);
                output.SaveAsPng(stream, output.Width, output.Height);
            }
            catch (Exception e)
            {
                Log.error("Exception: " + e);
            }
            finally
            {
                display.EndScene();
                if ( begun )
                    b.End();
                dev.SetRenderTarget(null);
                if ( stream != null )
                    stream.Dispose();
                if ( output != null )
                    output.Dispose();
            }
        }
    }
}
