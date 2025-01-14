using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using HelixToolkit.Wpf;
using System.ComponentModel.DataAnnotations;

namespace HoloTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //System.IO.File.AppendAllText("log.txt", "Twoja wiadomość\n");
            //System.Diagnostics.Debug.WriteLine("Twoja wiadomość debugowania");
            string dicomFolderPath = "C:\\Users\\picas\\Desktop\\NEural\\STU00001\\SER00001";
            double thresholdmin = -6;
            double thresholdmax = 4;
            //VolumeProcessor.Test("C:\\Users\\picas\\Desktop\\NEural\\STU00001\\SER00001\\IMG00001");
            double[,,] volumeData = VolumeProcessor.LoadVolume(dicomFolderPath, thresholdmin, thresholdmax);
            System.Diagnostics.Debug.WriteLine("------------");
            System.Diagnostics.Debug.WriteLine($"Min: {volumeData.Cast<double>().Min()}, Max: {volumeData.Cast<double>().Max()}");
            //Thread.Sleep(100);
            System.Diagnostics.Debug.WriteLine("------------");
            var mesh = VolumeRender.GenerateVolumeMesh(volumeData, thresholdmin, thresholdmax);
            var model = new GeometryModel3D
            {
                Geometry = mesh,
                Material = MaterialHelper.CreateMaterial(System.Windows.Media.Colors.Red)
            };
            viewport.Children.Add(new ModelVisual3D { Content = model });
        }
    }

    class VolumeProcessor
    {
        public static double[,,] LoadVolume(string path, double thresholdmin, double thresholdmax)
        {
            var files = System.IO.Directory.GetFiles(path);
            Array.Sort(files);
            int width = 0;
            int height = 0;
            int depth = files.Length;
            List<double[,]> slices = new List<double[,]>();
            foreach (var file in files)
            {
                var dicomFile = DicomFile.Open(file);
                var image = new DicomImage(dicomFile.Dataset);
                if (width == 0 || height == 0)
                {
                    width = image.Width;
                    height = image.Height;
                }
                var pixels = image.RenderImage().Pixels;
                double[,] slice = new double[width, height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        slice[x, y] = pixels[y * width + x];
                        
                    }
                }
                slices.Add(slice);
            }
            System.Diagnostics.Debug.WriteLine($"Width: {width}; Height: {height}; Depth: {depth}");
            double[,,] volume = new double[width, height, depth];
            for (int z = 0; z < depth; z+=10)
            {
                for (int y = 0; y < height; y+=10)
                {
                    for (int x = 0; x < width; x+=10)
                    {
                        if (slices[z][y, x] >= thresholdmin && slices[z][y, x] <= thresholdmax) 
                        {
                            volume[x, y, z] = slices[z][y, x];
                            System.Diagnostics.Debug.WriteLine($"V0: {volume[x, y, z]}");
                        }
                        //volume[x, y, z] = slices[z][y, x];
                        

                    }
                }
            }
            System.Diagnostics.Debug.WriteLine("------------");
            return volume;
        }
        public static void Test(string path)
        {
            
      
            string dicomFilePath = path;
            var dicomFile = DicomFile.Open(dicomFilePath);
            var image = new DicomImage(dicomFile.Dataset);

            System.Diagnostics.Debug.WriteLine("------------");
            System.Diagnostics.Debug.WriteLine($"Width: {image.Width}; Height: {image.Height}");

            // Pobierz dane pikseli
            var pixels = image.RenderImage().Pixels;
            int count = (image.Height-1)*image.Width+(image.Width -1);
            int[] ipix = new int[count];
            for(int i = 0 ; i < count-1; i++)
            {
                ipix[i] = pixels[i];
                
            }

            // Parametry histogramu
            int binSize = 10; // Rozmiar przedziału
            int minValue = ipix.Min(); // Minimalna wartość w danych
            int maxValue = ipix.Max(); // Maksymalna wartość w danych
            int numBins = (maxValue - minValue) / binSize + 1;

            // Tworzenie histogramu
            int[] histogram = new int[numBins];

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {

                    int pixelValue = pixels[y * image.Width + x];
                    if(pixelValue > -16777216) {
                        int binIndex = (pixelValue - minValue) / binSize;
                        histogram[binIndex]++; 
                    }
                    
                }
            }

            // Znajdź przedział z największą liczbą pikseli
            int maxCount = 0;
            int maxBinIndex = 0;

            for (int i = 0; i < histogram.Length; i++)
            {
                if (histogram[i] > maxCount)
                {
                    maxCount = histogram[i];
                    maxBinIndex = i;
                }
            }

            int rangeStart = minValue + maxBinIndex * binSize;
            int rangeEnd = rangeStart + binSize;

            System.Diagnostics.Debug.WriteLine($"Range with most pixels: {rangeStart} to {rangeEnd} (Count: {maxCount})");
        }

    }
    class VolumeRender
    {
        public static MeshGeometry3D GenerateVolumeMesh(double[,,] volumeData, double thresholdmin, double thresholdmax)
        {
            var meshBuilder = new MeshBuilder();
            int width = volumeData.GetLength(0);
            int height = volumeData.GetLength(1);
            int depth = volumeData.GetLength(2);
            System.Diagnostics.Debug.WriteLine($"Min: {volumeData.Cast<double>().Min()}, Max: {volumeData.Cast<double>().Max()}");
            for (int x = 0; x < width; x+=10)
            {
                for(int y = 0;y < height; y+=10)
                {
                    for(int z = 0; z < depth; z += 10)
                    {
                        //System.Diagnostics.Debug.WriteLine($"V1/2: {volumeData[x, y, z]}");
                        //if (volumeData[x, y, z] >= thresholdmin && volumeData[x, y, z] <= thresholdmax)
                        if (volumeData[x,y,z] == -1)
                        {
                            meshBuilder.AddBox(new Point3D(x,y,z), 1, 1, 1);
                            System.Diagnostics.Debug.WriteLine($"V1: {volumeData[x, y, z]}");
                        }
                    }
                }
            }
            return meshBuilder.ToMesh();

        }
    }
}