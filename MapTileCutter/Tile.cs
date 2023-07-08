using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace MapTileCutter
{
    public class Tile
    {
        #region Event
        public event EventHandler<TileSavedEventArgs> TileSaved;

        protected virtual void OnTileSaved(TileSavedEventArgs e)
        {
            EventHandler<TileSavedEventArgs> handler = TileSaved;
            handler?.Invoke(this, e);
        }
        #endregion

        public string Name { get; set; }
        public ImageFormat Format { get; set; }
        public Rectangle Rectangle { get; }
        public PixelFormat PixelFormat { get; }
        public DateTime StartAt { get; }

        public Tile(Rectangle rect, PixelFormat pixelFormat)
        {
            StartAt = DateTime.Now;
            Rectangle = rect;
            PixelFormat = pixelFormat;
        }

        public void Save(int level)
        {
            try
            {
                //Console.WriteLine($"level-{level},Name{Name},save rect:{Rectangle},now ImageSize:{Form1.MapImage.Size}");
                using (var tile = Form1.MapImage.Clone(Rectangle, PixelFormat)) // warning: out of memory error when rectangle area not in image area
                {

                    if (Name.Contains("/"))
                    {
                        //The last value from splitted name always will be the actual file name
                        //The rest will be the folder path
                        string[] split = Name.Split('/').Reverse().Skip(1).Reverse().ToArray();

                        string folder = String.Join("/", split);
                        if (!Directory.Exists(Path.Combine(Form1.ExportPathValue, folder)))
                        {
                            Directory.CreateDirectory(Path.Combine(Form1.ExportPathValue, folder));
                        }
                    }

                    tile.Save(Path.Combine(Form1.ExportPathValue, Name), Format);
                }

                GC.Collect();

                TileSavedEventArgs args = new TileSavedEventArgs()
                {
                    SavedAt = DateTime.Now,
                    Tile = this
                };
                OnTileSaved(args);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message}\r\n{ex.StackTrace}","Error");
                Application.Exit();
            }
        }
    }

    public class TileSavedEventArgs : EventArgs
    {
        public Tile Tile { get; set; }
        public DateTime SavedAt { get; set; }
    }
}
