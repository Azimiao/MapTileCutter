using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
//using System.Windows.Media.Imaging;
namespace MapTileCutter
{

    public partial class Form1 : Form
    {

        public static Bitmap MapImage;
        public static string ExportPathValue, BackgroundColor, SaveFormat;
        private static int MaxZoomLevel, MinZoomLevel, TileSize;
        private static ColorConverter ColorConverter = new ColorConverter();

        private static int TotalOfTilesToGenerate;
        private static int CurrentAmountOfTilesGenerated;
        /// <summary>
        /// The max width that bitMap can handle(Max 2GB,23170 is the max number in True Colorful Mode)
        /// </summary>
        private static int maxSizeOfBitMap = 23170;
        private static bool tileNumberPowerOf2 = true;
        public Form1()
        {
            InitializeComponent();
            structureComboBox.SelectedIndex = structureComboBox.Items.Count-1;
            TileFormat.SelectedIndex = TileFormat.Items.Count-1;

        }

        private void SelectMapImageButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    MapImage?.Dispose();

                    MapImagePath.Text = openFileDialog.FileName;
                }
            }    
        }

        private void ShowTooltip_MouseHover(object sender, EventArgs e)
        {
            BackgroundColorTooltip.Show("Color format: #ARGB (Alpha, Red, Green, Blue)", BackgroundColorLabel);
        }

        private void MakeTilesButton_Click(object sender, EventArgs e)
        {
            //if (!int.TryParse(MaxZoomTextBox.Text, out MaxZoomLevel))
            //{
            //    MessageBox.Show("Max Zoom is not a integer!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            //    return;
            //}

            tileNumberPowerOf2 = checkbox_align.Checked;

            if (!int.TryParse(MinZoomTextBox.Text, out MinZoomLevel))
            {
                MessageBox.Show("Min Zoom is not a integer!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!int.TryParse(TileSizeTextBox.Text, out TileSize))
            {
                MessageBox.Show("Tile size is not a integer!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if(TileSize % 2 != 0 || TileSize <= 0)
            {
                MessageBox.Show("Tile size wrong! must > 0 && can devi 2", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MapImagePath.Text.Length == 0 || !File.Exists(MapImagePath.Text))
            {
                MessageBox.Show("Map image path is empty or files does not exists!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if(ExportPath.Text.Length == 0 || !Directory.Exists(ExportPath.Text))
            {
                MessageBox.Show("Export path is empty or does not exists!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!ColorConverter.IsValid(BackgroundColorTextBox.Text))
            {
                MessageBox.Show("Background color is invalid.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);                
                return;
            }

            MakeTilesButton.Enabled = false;

            TotalOfTilesToGenerate = 0;
            CurrentAmountOfTilesGenerated = 0;

            string StructureFormat = structureComboBox.Text;
            BackgroundColor = BackgroundColorTextBox.Text;

            var image = (Bitmap)Bitmap.FromFile(MapImagePath.Text);

            MapImage = GetNewBitmapWithAdaptSize(image,ref MaxZoomLevel);


            if(MinZoomLevel > MaxZoomLevel)
            {
                MinZoomLevel = MaxZoomLevel;
            }

            SaveFormat = TileFormat.Text;

            ExportPathValue = ExportPath.Text;

            double _width = MapImage.Width;
            double _height = MapImage.Height;

            for (int i = MinZoomLevel; i <= MaxZoomLevel; i++)
            {
                TotalOfTilesToGenerate += int.Parse((Math.Ceiling(_width / TileSize) * Math.Ceiling(_height / TileSize)).ToString());

                _width /= 2;
                _height /= 2;
            }

            Task.Run(() =>
            {
                for (int zoomLevel = MaxZoomLevel; zoomLevel <= MaxZoomLevel; zoomLevel--)
                {
                    if (zoomLevel < MinZoomLevel)
                    {
                        break;
                    }
                    for (int x = 0; x < Math.Ceiling((double)MapImage.Width / TileSize); x++)
                    {
                        for (int y = 0; y < Math.Ceiling((double)MapImage.Height / TileSize); y++)
                        {
                            var rectangle = new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize);
                
                            Tile tile = new Tile(rectangle, PixelFormat.Format32bppArgb)
                            {
                                Name = $"{StructureFormat.Replace("x", x.ToString()).Replace("y", y.ToString()).Replace("z", (zoomLevel + 1).ToString()).Split('.')[0]}.{SaveFormat.ToLower()}",
                                Format = SaveFormat == "PNG" ? ImageFormat.Png : ImageFormat.Jpeg
                            };
                
                            tile.TileSaved += Tile_TileSaved;
                            tile.Save(zoomLevel);
                        }
                    }

                    MapImage = CropImage(MapImage, new Rectangle(0, 0, MapImage.Width, MapImage.Height), new Rectangle(0, 0, MapImage.Width / 2, MapImage.Height / 2),false);
                }
                MakeTilesButton.Invoke(new MethodInvoker(delegate { MakeTilesButton.Enabled = true; }));
                MapImage.Dispose();
                GC.Collect();
            });
        }

        private void Tile_TileSaved(object sender, TileSavedEventArgs e)
        {
            CurrentAmountOfTilesGenerated++;
            ProgressLabel.Invoke(new MethodInvoker(delegate { ProgressLabel.Text = $"Tile {e.Tile.Name} saved (Took {(e.SavedAt - e.Tile.StartAt).TotalMilliseconds}ms) ({CurrentAmountOfTilesGenerated} of {TotalOfTilesToGenerate})"; }));
        }

        private void ExportPathButton_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog openFolderDialog = new FolderBrowserDialog())
            {
                if (openFolderDialog.ShowDialog() == DialogResult.OK)
                {
                    ExportPath.Text = openFolderDialog.SelectedPath;
                }
            }
        }

        Bitmap GetNewBitmapWithAdaptSize(Image originImage,ref int bestLevel)
        {
            var maxSize = Math.Max(originImage.Width, originImage.Height);

            var maxLevel = (int)Math.Floor(Math.Log(maxSizeOfBitMap * 1.0f / TileSize, 2));

            var maxLevel2 = (int)Math.Floor(Math.Log(maxSize * 1.0f / TileSize, 2));

            var realLevel = Math.Min(maxLevel, maxLevel2);

            bestLevel = realLevel;

            var realMaxSize = (int)Math.Pow(2,realLevel) * TileSize;

            var croppedImage = new Bitmap(realMaxSize, realMaxSize);

            int targetWidth = realMaxSize;
            int targetHeight = realMaxSize;
            int centerX = 0;
            int centerY = 0;

            if(originImage.Width != originImage.Height)
            {
                var o = 1.0f * originImage.Width / originImage.Height;

                if (o > 1)
                {
                    targetHeight = (int)Math.Floor(realMaxSize / o);
                    centerY = (int)((realMaxSize - targetHeight) * 0.5f);
                }
                else
                {
                    targetWidth = (int)Math.Floor(realMaxSize * o);
                    centerX = (int)((realMaxSize - targetWidth) * 0.5f);
                }
            }
            string saveInfo = $@"Cut by Yetu's Tile Cutter @ {DateTime.Now.ToString("F")} 
File Calculate Info:
    FileName: {MapImagePath.Text}
    OriginSize: {originImage.Width} x {originImage.Height}
    TargetSize: {realMaxSize} x {realMaxSize}
    TileSize: {TileSize}
    TargetDrawOffset(LeftTop): {centerX} x {centerY}
    TargetDrawContentSize: {targetWidth} x {targetHeight}
            ";
            var savePath = Path.Combine(ExportPath.Text, Path.GetFileNameWithoutExtension(MapImagePath.Text)) + "_info.txt";
            Console.WriteLine(savePath);
            File.WriteAllText(savePath,saveInfo);
            Console.WriteLine($"we think size {realMaxSize} is best,and level = {bestLevel},offset:X-{centerX},Y-{centerY}");

            using (var graphics = Graphics.FromImage(croppedImage))
            {
                graphics.Clear((Color)ColorConverter.ConvertFromString(BackgroundColor));
                graphics.DrawImage(originImage, new Rectangle(centerX, centerY, targetWidth, targetHeight), new Rectangle(0,0,originImage.Width,originImage.Height), GraphicsUnit.Pixel);
                originImage.Dispose();
                GC.Collect();
            }



            return croppedImage;
        }

        Bitmap CropImage(Image originalImage, Rectangle sourceRectangle, Rectangle destinationRectangle,bool stillNeedOriginalImage = true,bool isFirst = false)
        {
            Rectangle _destRectancle = destinationRectangle;

            var croppedImage = new Bitmap(_destRectancle.Width, _destRectancle.Height);

            using (var graphics = Graphics.FromImage(croppedImage))
            {
                graphics.Clear((Color)ColorConverter.ConvertFromString(BackgroundColor));
                graphics.DrawImage(originalImage, destinationRectangle, sourceRectangle, GraphicsUnit.Pixel);
            }

            if (!stillNeedOriginalImage)
            {
                originalImage.Dispose();
                GC.Collect();
            }

            return croppedImage;
        }
    }
}
