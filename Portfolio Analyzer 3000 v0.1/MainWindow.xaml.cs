using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.IO;


namespace Portfolio_Analyzer_3000_v0._1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public struct price
        {
            public DateTime date;
            public float entryPrice;
            public float exitPrice;
            public float divPrice;
            public float realPrice;
            public string productID;
        }

        public struct productReturn
        {
            public string productID;
            public DateTime startDate;
            public DateTime endDate;
            public int month;
            public int year;
            public float startValue;
            public float endvalue;
            public float returnPercent;
            public string returnType;
        }

        public struct portfolio
        {
            public string[] products;
            public int[] prodIndexes;
            public float[] weightings;
            public float averageReturn;
            public float stdDeviation;
            public float sharpeRatio;
        }

        public struct product
        {
            public string productID;
            public string productName;
            public float adminPercent;
        }

        public struct graphSeries
        {
            string series;
            List<Point> points;
        }

        public struct graph
        {
            public int HorizMarkers;
            public int VertMarkers;
            public float xMin;
            public float xMax;
            public float yMin;
            public float yMax;
            public float colWidth;
            public float colSeperation;
            public float lineThick;
            public List<graphSeries> series;
            public int graphType;

        }

        public static string EXPORT_FOLDER = "C:\\ProgramData\\Portfolio Analyzer 3000\\Data Folder\\";
        public static string PRICE_FILE = "PriceList.priceList";
        public static string PORTFOLIO_FILE = "PortfolioList.folioList";

        public static float GRAPH_AXIS_SEPERATION = 20F;

        public static int MAX_INTERIM_FOLIOS = 1000;
        public static int GRAPH_TYPE_LINE = 1;
        public static int GRAPH_TYPE_COLUMN = 2;

        public static SolidColorBrush GREEN_BRUSH = new SolidColorBrush(Color.FromRgb(0, 200, 0));
        public static SolidColorBrush YELLOW_BRUSH = new SolidColorBrush(Color.FromRgb(255, 255, 0));
        public static SolidColorBrush RED_BRUSH = new SolidColorBrush(Color.FromRgb(200, 0, 0));
        public static SolidColorBrush BLUE_BRUSH = new SolidColorBrush(Color.FromRgb(0, 150, 255));
        public static SolidColorBrush GREY_BRUSH = new SolidColorBrush(Color.FromRgb(212, 212, 212));
        public static SolidColorBrush BLACK_BRUSH = new SolidColorBrush(Color.FromRgb(0, 0, 0));

        HtmlAgilityPack.HtmlWeb web = new HtmlAgilityPack.HtmlWeb();
        List<List<price>> productPrices = new List<List<price>>();
        List<List<productReturn>> productYearlyReturns = new List<List<productReturn>>();
        List<List<productReturn>> productMonthlyReturns = new List<List<productReturn>>();
        List<List<portfolio>> PortfolioList;
        List<portfolio> InterimPortfolioList;
        List<portfolio> FinalPortfolioList;
        float[,] CovarianceMatrix;
        List<float[]> WeightingMatrix;
        float[,] realReturns;
        float[] averageReturns;
        float[] standardDevs;
        List<product> products;
        List<int[]> trimmedProdList;
        


        IFormatProvider culture = new System.Globalization.CultureInfo("en-AU", true);


        string priceExportFolder = "";
        string portfolioExportFolder = "";
        int maxThreads = 5;
        int runningThreads = 0;
        int maxPortfolios = 1;
        int portfoliothreadCount;
        int maxThreadCount = Environment.ProcessorCount;
        long estimatedPortfolioTests = 0;
        long numChecked = 0;
        float riskFreeRate = 0;
        float maxRiskLevel = 0;
        float requiredReturn = 0;
        DateTime firstReturnDate;
        DateTime lastReturnDate;
        DateTime calcStartTime;
        DateTime startDate;
        DateTime endDate;
        TimeSpan remainTime;
        TimeSpan timetaken;

        bool loadedPrices = false;
        bool loadedProducts = false;
        bool calculatedReturns = false;
        bool calculatedAverageReturns = false;
        bool calculatedCovariances = false;
        bool calculatedWeights = false;
        bool calculatedTrimProdList = false;
        bool autoCalcPortfolios = false;
        bool quickCalcPortfolios = false;
        bool riskLevelCalculation = false;
        bool returnLevelCalculation = false;
        bool cancelCalcs = false;

        int[] skipped;

        public MainWindow()
        {
            InitializeComponent();
            if(maxThreadCount > 8) { maxThreadCount = 8; }
            
        }

        private void Btn_LoadPriceList_Click(object sender, RoutedEventArgs e)
        {
            loadedPrices = false;
            calculatedReturns = false;
            calculatedAverageReturns = false;
            calculatedCovariances = false;
            calculatedWeights = false;
            Thread tempThread = new Thread(() => loadInPrices());
            tempThread.IsBackground = true;
            tempThread.Start();
        }

        public string pickFile(string initalDir, string filter)
        {
            string filename = "";
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            if(!Directory.Exists(initalDir))
            {
                initalDir = "";
            }

            dlg.InitialDirectory = initalDir;
            dlg.Filter = filter;

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();

            if(result == true)
            {
                filename = dlg.FileName;
            }
            else
            {
                filename = null;
            }

            return filename;
            
        }

        public void loadInPrices()
        {
            string filename = pickFile(EXPORT_FOLDER, "Price Lists | *.priceList");

            if(filename == null)
            {
                this.Dispatcher.Invoke(() =>
                {
                    addStatusLine("No File Selected");
                });
                return;
            }
            else
            {
                this.Dispatcher.Invoke(() =>
                {
                    lbl_PriceListFile.Text = filename;
                });
            }

            this.Dispatcher.Invoke(() =>
            {
                addStatusLine("Loading price list");
            });


            string path = System.IO.Path.GetDirectoryName(filename);

            productPrices = new List<List<price>>();

            string[] productIDList = importProductList(filename);

            this.Dispatcher.Invoke(() =>
            {
                addStatusLine("Loading " + productIDList.Count().ToString() + " products");
            });

            for (int i = 0; i < productIDList.Count(); i++)
            {
                productPrices.Add(importPriceList(path + "\\" + productIDList[i] + ".csv"));
            }

            this.Dispatcher.Invoke(() =>
            {
                updateKeyStats();
                addStatusLine("Load complete");
            });

            loadedPrices = true;
        }

        public string setupExportFolder(string location, string type)
        {
            string exportFolder = location + "\\" + type + "\\"+DateTime.Now.ToString("yyyyMMdd HH.mm.ss");

            Directory.CreateDirectory(exportFolder);

            return exportFolder;


        }

        public void getPrices(int startId = 0, int endId = 999, int threadID = 0)
        {
            Ellipse ThreadStatusElipse = null;

            switch (threadID)
            {
                case 0:
                    ThreadStatusElipse = Ell_DwnThread0;
                    break;
                case 1:
                    ThreadStatusElipse = Ell_DwnThread1;
                    break;
                case 2:
                    ThreadStatusElipse = Ell_DwnThread2;
                    break;
                case 3:
                    ThreadStatusElipse = Ell_DwnThread3;
                    break;
                case 4:
                    ThreadStatusElipse = Ell_DwnThread4;
                    break;
            }

            for (int prodID = startId; prodID <= endId; prodID++)
            {
                this.Dispatcher.Invoke(() =>
                {
                    addStatusLine("Thread: " + threadID + " Starting load: " + prodID);
                    ThreadStatusElipse.Fill = GREEN_BRUSH;
                }
                );

                List<price> prices = new List<price>();
                prices = loadPrices(prodID.ToString());
                if (prices.Count == 0)
                {
                    string statusLine = "Thread: " + threadID + " product null: " + prodID;
                    Console.WriteLine(statusLine);
                    this.Dispatcher.Invoke(() =>
                    {
                        addStatusLine(statusLine);
                        updateKeyStats();
                    }
                    );
                }
                else
                {
                    string statusLine = "Thread: " + threadID + " loaded product: " + prodID;
                    Console.WriteLine(statusLine);
                    this.Dispatcher.Invoke(() =>
                    {
                        addStatusLine(statusLine);
                        updateKeyStats();
                    }
                    );
                    productPrices.Add(calcRealPrices(prices));
                    
                }

            }

            this.Dispatcher.Invoke(() =>
            {
                addStatusLine("Thread: " + threadID + " fetch complete");
            }
            );

            runningThreads -= 1;

            this.Dispatcher.Invoke(() =>
            {
                Txt_RunningThreads.Text = runningThreads.ToString();
                updateKeyStats();
            }
            );


            if (runningThreads == 0)
            {
                this.Dispatcher.Invoke(() =>
                {
                    addStatusLine("All fetch threads closed, de-duplicating product prices.");
                    updateKeyStats();
                });

                productPrices = deDuplicateProducts(productPrices);

                this.Dispatcher.Invoke(() =>
                {
                    addStatusLine("De-duplication complete");
                    updateKeyStats();
                    addStatusLine("Exporting Price Data");
                });

                for(int i = 0; i < productPrices.Count(); i++)
                {
                    exportPriceList(priceExportFolder + "\\" + productPrices[i][0].productID + ".csv", productPrices[i]);
                }

                exportProductList(productPrices, priceExportFolder + "\\" + PRICE_FILE);

                /*
                if (MessageBox.Show("Generate New Product List File?", "New Product File" ,MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes)
                {
                    //yes
                    exportProductList(productPrices);
                }
                */
            }

        }

        public List<price> loadPrices(string inProductID)
        {
            List<price> prices = new List<price>();

            string webAddress = "http://www.colonialfirststate.com.au/Price_performance/HistoricalUnitPrices.aspx?MainGroup=IF&GroupID=91&ProductID=" + inProductID + "&Public=1&FundTransfer=false";

            HtmlAgilityPack.HtmlDocument doc;
            try
            {
                doc = web.Load(webAddress);
            }
            catch(Exception e)
            {
                return prices;
            }

            if (web.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Console.WriteLine("invalid product ID: " + inProductID);
                return null;
            }
            
            foreach (HtmlAgilityPack.HtmlNode tbl in doc.DocumentNode.SelectNodes("//table"))
            {
                if (tbl.Id != "grdPrices")
                {
                    continue;
                }

                foreach (HtmlAgilityPack.HtmlNode row in tbl.SelectNodes("tr"))
                {
                    price tempPrice = new price();

                    var cell = row.SelectNodes("th|td").ToArray();
                    if (cell[0].InnerText.Trim() == "Effective Date (Sort Ascending)")
                    {
                        continue;
                    }
                    tempPrice.productID = inProductID;
                    tempPrice.date = Convert.ToDateTime(cell[0].InnerText.Trim(), culture);
                    tempPrice.entryPrice = float.Parse(cell[1].InnerText.Trim());
                    if (cell[2].InnerText.Trim() == "pre-incomepost-income")
                    {
                        tempPrice.divPrice = float.Parse(cell[3].InnerText.Trim().Substring(0, 6));
                        tempPrice.exitPrice = float.Parse(cell[3].InnerText.Trim().Substring(6, 6));
                    }
                    else
                    {
                        tempPrice.exitPrice = float.Parse(cell[3].InnerText.Trim());
                    }
                    
                    prices.Add(tempPrice);
                }
            }

            return prices;
        }

        public List<price> calcRealPrices(List<price> inPrices)
        {
            List<price> prices = new List<price>();

            inPrices.Reverse();

            float totalDiv = 0.0F;

            foreach (price price in inPrices)
            {
                if (price.divPrice != 0)
                {
                    totalDiv += price.divPrice - price.exitPrice;
                }
                price tempPrice = new price();
                tempPrice.date = price.date;
                tempPrice.productID = price.productID;
                tempPrice.divPrice = price.divPrice;
                tempPrice.entryPrice = price.entryPrice;
                tempPrice.exitPrice = price.exitPrice;
                tempPrice.realPrice = price.exitPrice + totalDiv;
                prices.Add(tempPrice);

            }

            return prices;
        }

        private void addStatusLine(string line)
        {
            Txt_StatusBox.AppendText(line);
            Txt_StatusBox.AppendText("\u2028");
            Txt_StatusBox.ScrollToEnd();
        }

        private void exportPriceList(string outFile, List<price> inPrices)
        {
            string[] temp = new string[inPrices.Count];
            for(int i = 0; i < inPrices.Count; i++)
            {
                temp[i] = inPrices[i].date.ToString(culture) + ",";
                temp[i] += inPrices[i].entryPrice.ToString() + ",";
                temp[i] += inPrices[i].exitPrice.ToString() + ",";
                temp[i] += inPrices[i].divPrice.ToString() + ",";
                temp[i] += inPrices[i].productID.ToString() + ",";
                temp[i] += inPrices[i].realPrice.ToString();
            }

            File.WriteAllLines(outFile, temp);

        }

        private void Txt_StatusBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Btn_DownloadProducts_Click(object sender, RoutedEventArgs e)
        {

            if(!validProductIDs()) { return; }

            productPrices = new List<List<price>>();

            int startID = int.Parse(txt_ProductStartID.Text);
            int endID = int.Parse(txt_ProductEndID.Text);

            int step = (int)Math.Ceiling((float)(endID - startID) / (float)maxThreads);
            if (step == 0)
            {
                step = 1;
            }
            if (startID < 0) { startID = 0; }
            if (endID > 999) { endID = 999; }
            int threadID = 0;

            addStatusLine("Setting up Prices Export Folder");

            priceExportFolder = setupExportFolder(EXPORT_FOLDER, "Prices");

            addStatusLine("Fetching Product Details: " + startID.ToString() + " to " + endID.ToString() + "(" + Math.Floor((float)(endID - startID) / step).ToString() + " threads)");


            for (int i = startID; i <= endID; i += step)
            {
                int temp = i;
                int temp2 = threadID;
                Thread getPriceThread = new Thread(() => getPrices(temp, (temp + step - 1), temp2));
                getPriceThread.IsBackground = true;
                getPriceThread.Start();
                threadID++;
                runningThreads += 1;

                Txt_RunningThreads.Text = runningThreads.ToString();

            }
        }

        private bool validProductIDs()
        {
            if (txt_ProductStartID.Text.Length == 0 || txt_ProductEndID.Text.Length == 0)
            {
                addStatusLine("Ensure both StartID and EndID are set to positive numbers");
                return false;
            }


            if (!(txt_ProductStartID.Text.All(char.IsDigit) && txt_ProductEndID.Text.All(char.IsDigit)))
            {
                addStatusLine("Ensure both StartID and EndID are set to positive numbers");
                return false;
            }

            int startID = int.Parse(txt_ProductStartID.Text);
            int endID = int.Parse(txt_ProductEndID.Text);

            if (endID <= startID)
            {
                addStatusLine("Ensure both EndID is greater than StartID");
                return false;
            }


            return true;


        }
        

        public List<List<price>> deDuplicateProducts(List<List<price>> inProductPrices)
        {
            List<List<price>> newList = new List<List<price>>();

            for(int i = 0; i < inProductPrices.Count(); i++)
            {
                bool duplicate = false;

                for (int j = 0; j < newList.Count(); j++) //check through all new list first to ensure not adding a duplicate
                {
                    if(inProductPrices[i][0].date == newList[j][0].date) //if first date same
                    {
                        if (inProductPrices[i][inProductPrices[i].Count() - 1].date == newList[j][newList[j].Count() - 1].date) //if last date same
                        {
                            if (inProductPrices[i][inProductPrices[i].Count() - 1].realPrice == newList[j][newList[j].Count() - 1].realPrice) //if last real price same
                            {
                                //if all the above true then its a duplicate
                                duplicate = true;
                                break;
                            } 
                        }
                    }
                }

                if (!duplicate)
                {
                    newList.Add(inProductPrices[i]);
                }

            }

            return newList;
        }

        public void updateKeyStats()
        {
            if(productPrices.Count() > 0)
            {
                txt_TotalProducts.Text = productPrices.Count().ToString();

                //get the total number of prices in all of the products
                int totalPrices = 0;
                foreach (var list in productPrices)
                {
                    foreach (var item in list)
                    {
                        totalPrices++;
                    }
                }
                
                txt_TotalPrices.Text = totalPrices.ToString("#,###");
            }

            
        }

        public void exportProductList(List<List<price>> inProductPrices)
        {
            string[] exportString;

            exportString = new string[inProductPrices.Count()];
            for(int i = 0; i < inProductPrices.Count(); i++)
            {
                exportString[i] = inProductPrices[i][0].productID.ToString();
            }

            File.WriteAllLines(EXPORT_FOLDER + DateTime.Now.ToString("yyyyMMdd hh.mm.ss ")+ " " + PRICE_FILE, exportString);

        }

        public void exportProductList(List<List<price>> inProductPrices, string outFile)
        {
            string[] exportString;

            exportString = new string[inProductPrices.Count()];
            for (int i = 0; i < inProductPrices.Count(); i++)
            {
                exportString[i] = inProductPrices[i][0].productID.ToString();
            }

            File.WriteAllLines(outFile, exportString);

        }

        public string[] importProductList(string inFile)
        {
            return File.ReadAllLines(inFile);
        }

        public List<price> importPriceList(string inFile)
        {
            List<price> newPrices = new List<price>();

            string[] inPrices = File.ReadAllLines(inFile);

            for(int i = 0; i < inPrices.Count(); i++)
            {
                price newPrice = new price();
                string[] temp = inPrices[i].Split(',');


                newPrice.date = Convert.ToDateTime(temp[0], culture);
                newPrice.entryPrice = float.Parse(temp[1]);
                newPrice.exitPrice = float.Parse(temp[2]);
                newPrice.divPrice = float.Parse(temp[3]);
                newPrice.productID = temp[4];
                newPrice.realPrice = float.Parse(temp[5]);

                newPrices.Add(newPrice);

            }


            return newPrices;


        }

        private void Btn_DeDuplicate_Click(object sender, RoutedEventArgs e)
        {

            if (!loadedPrices)
            {
                addStatusLine("Load products first!");
                return;
            }

            addStatusLine("De-Duplicating");

            productPrices = deDuplicateProducts(productPrices);
            
            updateKeyStats();

            addStatusLine("De-Duplication complete");
        }

        private void Btn_SaveProductList_Click(object sender, RoutedEventArgs e)
        {

            if (!loadedPrices)
            {
                addStatusLine("Load products first!");
                return;
            }

            addStatusLine("Saving Product list");

            priceExportFolder = setupExportFolder(EXPORT_FOLDER, "Prices");

            for(int i = 0; i < productPrices.Count(); i++)
            {
                exportPriceList(priceExportFolder + "\\" + productPrices[i][0].productID + ".csv", productPrices[i]);
            }
            
            exportProductList(productPrices, priceExportFolder + "\\" + PRICE_FILE);

            addStatusLine("Save complete");
        }

        private List<List<productReturn>> calculateReturns(List<List<price>> inProductPrices, string timeScale)
        {
            List<List<productReturn>> returns = new List<List<productReturn>>();

            int minDay = 99999;

            if (timeScale == "yearly")
            {
                minDay = 329; // 90% of a year
            }
            else if (timeScale == "monthly")
            {
                minDay = 24; //80% of a month (30 days)
            }


            for (int i = 0; i < inProductPrices.Count(); i++)
            {
                DateTime startDate = inProductPrices[i][0].date;
                if((startDate > Convert.ToDateTime("01/01/2000",culture)) && (startDate < firstReturnDate) || (firstReturnDate == Convert.ToDateTime("01/01/0001", culture))) { firstReturnDate = startDate; }
                DateTime endDate;

                if(timeScale == "yearly")
                {
                    endDate = Convert.ToDateTime("01/01/" + (startDate.Year + 1).ToString(), culture);
                }
                else if (timeScale == "monthly")
                {
                    if (startDate.Month == 12)
                    {
                        endDate = Convert.ToDateTime("01/01/" + (startDate.Year + 1).ToString(), culture);
                    }
                    else
                    {
                        endDate = Convert.ToDateTime("01/" + (startDate.Month + 1 ).ToString() + "/" + (startDate.Year).ToString(), culture);
                    }
                }

                
                int startIndex = 0;

                List<productReturn> newpriceList = new List<productReturn>();

                for (int j = 0; j < inProductPrices[i].Count(); j++)
                {
                    productReturn tempReturn = new productReturn();
                    if ((inProductPrices[i][j].date.Year != inProductPrices[i][startIndex].date.Year && timeScale == "yearly") || (inProductPrices[i][j].date.Month != inProductPrices[i][startIndex].date.Month && timeScale == "monthly"))
                    {
                        if ((inProductPrices[i][j].date - inProductPrices[i][startIndex].date).Days >= minDay)
                        {
                            tempReturn.startDate = inProductPrices[i][startIndex].date;
                            tempReturn.startValue = inProductPrices[i][startIndex].realPrice;
                            tempReturn.endDate = inProductPrices[i][j].date;
                            tempReturn.endvalue = inProductPrices[i][j].realPrice;
                            if (timeScale == "yearly") { tempReturn.year = inProductPrices[i][startIndex].date.Year; }
                            if (timeScale == "monthly") { tempReturn.month = inProductPrices[i][startIndex].date.Month; }
                            tempReturn.productID = inProductPrices[i][startIndex].productID;
                            tempReturn.returnPercent = (tempReturn.endvalue / tempReturn.startValue) - 1;
                            tempReturn.returnType = timeScale;

                            newpriceList.Add(tempReturn);

                        }
                        startIndex = j;
                    }
                }

                if (newpriceList.Count() > 0)
                {
                    returns.Add(newpriceList);
                    if(newpriceList[newpriceList.Count()-1].endDate > lastReturnDate) { lastReturnDate = newpriceList[newpriceList.Count() - 1].endDate; }
                }
            }

            this.Dispatcher.Invoke(() =>
            {
                Dte_StartDate.DisplayDateStart = firstReturnDate;
                Dte_StartDate.DisplayDateEnd = lastReturnDate;
                Dte_EndDate.DisplayDateStart = firstReturnDate;
                Dte_EndDate.DisplayDateEnd = lastReturnDate;
            });

            return returns;
        }

        private void Btn_CalculateReturns_Click(object sender, RoutedEventArgs e)
        {
            if (!loadedPrices)
            {
                addStatusLine("Load products first!");
                return;
            }
           
            Thread tempThread = new Thread(() => calcReturns());
            tempThread.IsBackground = true;
            tempThread.Start();

        }

        private void calcReturns()
        {
            this.Dispatcher.Invoke(() =>
            {
                addStatusLine("Calculating Monthly Returns");
            });

            productMonthlyReturns = calculateReturns(productPrices, "monthly");

            int totalReturnCount = 0;
            for(int i = 0; i < productMonthlyReturns.Count(); i++)
            {
                totalReturnCount += productMonthlyReturns[i].Count();
            }

            this.Dispatcher.Invoke(() =>
            {
                addStatusLine("Return Calculations Complete");
                txt_TotalReturns.Text = totalReturnCount.ToString("#,###");
            });

            calculatedReturns = true;
        }

        private void calcRealReturns(List<List<productReturn>> inReturns, DateTime startDate, DateTime endDate)
        {
            int numMonths = 0;
            if (endDate.Month >= startDate.Month)
            {
                numMonths = (endDate.Year - startDate.Year) * 12;
                numMonths += (endDate.Month - startDate.Month) + 1;
            }
            else
            {
                numMonths = (endDate.Year - startDate.Year) * 12;
                numMonths -= ((startDate.Month - endDate.Month) - 1);
            }

            realReturns = new float[inReturns.Count(), numMonths];
            DateTime[] returnMonths = new DateTime[numMonths];

            for (int i = 0; i < numMonths; i++)
            {
                DateTime temp = startDate.AddMonths(i);
                returnMonths[i] = temp.AddDays(-(temp.Day - 1));
            }

            for (int i = 0; i < inReturns.Count(); i++)
            {
                for (int j = 0; j < returnMonths.Count(); j++)
                {
                    for (int k = 0; k < inReturns[i].Count(); k++)
                    {
                        if (inReturns[i][k].startDate.Month == returnMonths[j].Month && inReturns[i][k].startDate.Year == returnMonths[j].Year)
                        {
                            realReturns[i, j] = inReturns[i][k].returnPercent - getMonthlyAdmin(productMonthlyReturns[i][0].productID);
                            break;
                        }
                    }
                }
            }
        }

        private void calcAverageReturns()
        {
            averageReturns = new float[realReturns.GetUpperBound(0) + 1];

            for (int i = 0; i < realReturns.GetUpperBound(0)+1;i++)
            {
                averageReturns[i] = 0;
                for (int j =0; j < realReturns.GetUpperBound(1)+1; j++)
                {
                    averageReturns[i] += realReturns[i, j];
                }
                averageReturns[i] = averageReturns[i] / (realReturns.GetUpperBound(1) + 1);
            }
        }

        private void Btn_Date1Year_Click(object sender, RoutedEventArgs e)
        {
            
            DateTime tempDate = lastReturnDate.AddDays(-lastReturnDate.Day);
            Dte_StartDate.SelectedDate = tempDate.AddYears(-1);
            Dte_EndDate.SelectedDate = tempDate;
        }

        private void Btn_Date3Year_Click(object sender, RoutedEventArgs e)
        {
            
            DateTime tempDate = lastReturnDate.AddDays(-lastReturnDate.Day);
            Dte_StartDate.SelectedDate = tempDate.AddYears(-3);
            Dte_EndDate.SelectedDate = tempDate;
        }

        private void Btn_Date5Year_Click(object sender, RoutedEventArgs e)
        {
           
            DateTime tempDate = lastReturnDate.AddDays(-lastReturnDate.Day);
            Dte_StartDate.SelectedDate = tempDate.AddYears(-5);
            Dte_EndDate.SelectedDate = tempDate;
        }

        private void Btn_CalculateCovariances_Click(object sender, RoutedEventArgs e)
        {
            if (!calculatedAverageReturns)
            {
                addStatusLine("Calculate Average Returns first!");
                return;
            }
            

            Thread tempThread = new Thread(() => calcCoVar(startDate, endDate));
            tempThread.IsBackground = true;
            tempThread.Start();

        }

        private void calcCoVar(DateTime startDate, DateTime endDate)
        {
            this.Dispatcher.Invoke(() =>
            {
                addStatusLine("Calculating Covariance Matrix");
            });

            CovarianceMatrix = CalculateCoVariances(productMonthlyReturns, startDate, endDate);

            this.Dispatcher.Invoke(() =>
            {
                addStatusLine("Calculating Covariance Matrix Completed");
            });

            calculatedCovariances = true;
        }

        private float[,] CalculateCoVariances(List<List<productReturn>> inReturns, DateTime startDate, DateTime endDate)
        {
            float[,] outMatrix = new float[inReturns.Count(), inReturns.Count()];

            int numMonths = 0;
            if(endDate.Month >= startDate.Month)
            {
                numMonths = (endDate.Year - startDate.Year) * 12;
                numMonths += (endDate.Month - startDate.Month)+1;
            }
            else
            {
                numMonths = (endDate.Year - startDate.Year) * 12;
                numMonths -= ((startDate.Month - endDate.Month)-1);
            }

            int minDataPoints = (int)(0.8F * numMonths);

            

            float xMean = 0.0F;
            float yMean = 0.0F;
            int xCount = 0;
            int yCount = 0;


            float sumXYVar = 0;

            for(int i =0; i < inReturns.Count(); i++)
            {
                xCount = 0;
                xMean = 0;
                for (int k = 0; k < realReturns.GetUpperBound(1)+1; k++)
                {
                    if(realReturns[i,k] != 0.0F)
                    {
                        xCount++;
                        xMean += realReturns[i, k];
                    }
                }
                xMean = xMean / (float)xCount;

                for (int j = 0; j < inReturns.Count(); j++)
                {
                    if(xCount > minDataPoints)
                    {
                        yCount = 0;
                        yMean = 0;
                        for (int k = 0; k < realReturns.GetUpperBound(1)+1; k++)
                        {
                            if (realReturns[j, k] != 0.0F)
                            {
                                yCount++;
                                yMean += realReturns[j, k];
                            }
                        }
                        yMean = yMean / (float)yCount;
                        if (yCount > minDataPoints)
                        {
                            sumXYVar = 0.0F;
                            for (int k = 0; k < realReturns.GetUpperBound(1)+1; k++)
                            {
                                sumXYVar += (realReturns[i, k] - xMean) * (realReturns[j, k] - yMean);
                            }
                            outMatrix[i, j] = sumXYVar / (realReturns.GetUpperBound(1));

                        }
                        else
                        {
                            outMatrix[i, j] = 0;
                        }
                    }
                    else
                    {
                        outMatrix[i, j] = 0;
                    }

                }

            }

            return outMatrix;
        }

        private float calcMean(float[] inValues)
        {
            float mean = 0;

            for (int i = 0; i < inValues.Count(); i++)
            {
                mean += inValues.Sum();
            }

            return mean;
        }

        private void Btn_GenerateWeightings_Click(object sender, RoutedEventArgs e)
        {
            if (!calculatedCovariances)
            {
                addStatusLine("Calculate Co-Variances first!");
                return;
            }

            int prods = (int)Sld_NumProducts.Value;


            if((bool)ChkBx_SingleWeighting.IsChecked)
            {
                WeightingMatrix = new List<float[]>();

                float[] tempWeights = new float[prods];

                for (int i = 0; i < prods; i++)
                {
                    tempWeights[i] = (1F / (float)prods);
                }

                WeightingMatrix.Add(tempWeights);

                txt_TotalWeights.Text = WeightingMatrix.Count().ToString("#,###");

                calculatedWeights = true;

                addStatusLine("Weights calculated");
            }
            else
            {
                float weightStep = (float)Math.Round(Sld_PercentageStep.Value / 100, 2);


                Thread tempThread = new Thread(() => genWeights(prods, weightStep));
                tempThread.IsBackground = true;
                tempThread.Start();
            }


        }

        private void genWeights(int numProds, float weightStep)
        {
            this.Dispatcher.Invoke(() =>
            {
                addStatusLine("Generating Weights Matrix");
            });
            WeightingMatrix = generateWeights(numProds, weightStep);

            this.Dispatcher.Invoke(() =>
            {
                addStatusLine("Weights Generation Complete");
                txt_TotalWeights.Text = WeightingMatrix.Count().ToString("#,###");
            });

            calculatedWeights = true;

        }

        private List<float[]> generateWeights(int numProds, float weightStep)
        {
            List<float[]> weights = new List<float[]>();

            if(numProds < 1)
            {
                this.Dispatcher.Invoke(() =>
                {
                    addStatusLine("Cannot have less than a single product");
                });
                return null;
            }
            if (numProds > 20)
            {
                this.Dispatcher.Invoke(() =>
                {
                    addStatusLine("Cannot have over 20 products");
                });
                return null;
            }
            if(numProds == 1)
            {
                float[] tempWeights = new float[numProds];
                tempWeights[0] = 1;
                weights.Add(tempWeights);
            }
            else
            {
                float[] tempArr = new float[numProds];
                weights = weightsRecur(0, numProds, tempArr, weights, weightStep);
            }

            return weights;
        }

        private List<float[]> weightsRecur(int layer, int targetDepth, float[] array, List<float[]> weightsList, float weightStep)
        {
            for(float i = 0; i <= 1; i = (float)Math.Round(i + weightStep,2))
            {
                array[layer] = i;
                if((layer < targetDepth-1) && array.Sum() <= 1)
                {
                    weightsList = weightsRecur(layer + 1, targetDepth, array, weightsList, weightStep);
                }
                else if(layer == targetDepth-1)
                {
                    if (array.Sum() == 1)
                    {
                        float[] temp = new float[array.Count()];
                        for (int j = 0; j < array.Count(); j++)
                        {
                            temp[j] = (float)Math.Round(array[j], 2);
                        }
                        weightsList.Add(temp);
                        array[layer] = 0;
                        if(weightsList.Count() % 10000 == 0)
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                txt_TotalWeights.Text = weightsList.Count().ToString("#,###");
                            });
                        }
                        
                    }
                }
            }
            array[layer] = 0;
            return weightsList;
        }

        private void generatePortfolios(int numProds, int startProdIndex, int endProdIndex, int threadIndex, int totalThreads, Ellipse ThreadStatusElipse = null)
        {
            /*
            this.Dispatcher.Invoke(() =>
            {
                if (ThreadStatusElipse != null)
                {
                    ThreadStatusElipse.Fill = GREEN_BRUSH;
                }
                addStatusLine("Generating Portfolio List");
            });
            */

            for (int j = 0; j < WeightingMatrix.Count(); j++)
            {
                if(cancelCalcs)
                {
                    return;
                }

                int[] array = new int[numProds];

                productRecur(0, numProds, array, startProdIndex, endProdIndex, WeightingMatrix[j], threadIndex);
            }

            

            this.Dispatcher.Invoke(() =>
            {
                if (ThreadStatusElipse != null)
                {
                    ThreadStatusElipse.Fill = GREY_BRUSH;
                }
                //addStatusLine("Generating Portfolio List Completed");
            });
            runningThreads--;
            

            

        }




       
        private void productRecur(int layer, int targetDepth, int[] array, int startIndex, int endIndex, float[] weightList, int threadIndex)
        {
            for(int i = startIndex; i <= endIndex; i++)
            {
                if(cancelCalcs)
                {
                    return;
                }

                // new optimisation code
                if (averageReturns[i] < 0)
                {
                    skipped[layer]++;
                    continue;
                }
                //////////

                array[layer] = i;

                if(layer < targetDepth-1)
                {
                    productRecur(layer + 1, targetDepth, array, i+1, CovarianceMatrix.GetUpperBound(0), weightList, threadIndex);
                }
                else if(layer == targetDepth - 1)
                {
                    checkPortfolio(array, weightList, threadIndex);
                    numChecked++;
                    estimatedPortfolioTests--;

                    /*

                    if (numChecked % 50000 == 0)
                    {
                        DateTime tempTime = DateTime.Now;
                        timetaken = tempTime - calcStartTime;
                        double percentComp = (double)numChecked / (double)(numChecked + estimatedPortfolioTests);
                        if (percentComp > 0)
                        {
                            double millisecondsTotal = timetaken.TotalMilliseconds / percentComp;
                            DateTime temp = DateTime.Now;
                            if (millisecondsTotal > int.MaxValue)
                            {
                                temp = calcStartTime.AddMilliseconds(int.MaxValue);
                            }
                            else
                            {
                                temp = calcStartTime.AddMilliseconds(millisecondsTotal);
                            }
                            
                            remainTime = temp - tempTime;
                            
                        }

                        this.Dispatcher.Invoke(() =>
                        {
                            txt_TotalPortfolios.Text = numChecked.ToString("#,###");
                            txt_TotalPortfoliosRemaining.Text = estimatedPortfolioTests.ToString("#,###");
                            txt_TimeRemaining.Text = remainTime.ToString(@"dd\:hh\:mm\:ss");
                            txt_TimeElapsed.Text = timetaken.ToString(@"dd\:hh\:mm\:ss");
                        });
                    }

                    */

                }

            }
            return;
            
        }
        
        private void checkPortfolio(int[] productArray, float[] weightArray, int threadIndex)
        {
            portfolio tempPortfolio = new portfolio();
            string[] products = new string[productArray.Count()];
            float[] temp = new float[weightArray.Count()];

            for (int i = 0; i < weightArray.Count(); i++)
            {
                temp[i] = weightArray[i];
            }
            tempPortfolio.weightings = temp;

            int[] temp2 = new int[productArray.Count()];

            for (int i = 0; i < productArray.Count(); i++)
            {
                temp2[i] = productArray[i];
            }
            tempPortfolio.prodIndexes = temp2;

            for (int i = 0; i < productArray.Count(); i++)
            {

                products[i] = productMonthlyReturns[productArray[i]][0].productID;

                tempPortfolio.averageReturn += (averageReturns[productArray[i]] * weightArray[i]);

                tempPortfolio.stdDeviation += (float)Math.Pow(standardDevs[productArray[i]] * weightArray[i], 2);

            }


            tempPortfolio.averageReturn -= riskFreeRate;


            int[] tempArr = new int[2];
            tempPortfolio.stdDeviation += coVarVarianceRecur(0, 2, 0, weightArray, productArray, 0F, tempArr);
            tempPortfolio.stdDeviation = (float)Math.Sqrt(tempPortfolio.stdDeviation);

            tempPortfolio.sharpeRatio = tempPortfolio.averageReturn / tempPortfolio.stdDeviation;

            tempPortfolio.products = products;

            if(PortfolioList[threadIndex].Count() == 0)
            {
                if (riskLevelCalculation)
                {
                    if (tempPortfolio.stdDeviation <= maxRiskLevel)
                    {
                        PortfolioList[threadIndex].Add(tempPortfolio);
                    }
                }
                else if (returnLevelCalculation)
                {
                    if (tempPortfolio.averageReturn >= requiredReturn)
                    {
                        PortfolioList[threadIndex].Add(tempPortfolio);
                    }

                }
                else
                {
                    PortfolioList[threadIndex].Add(tempPortfolio);
                }
            }
            else
            {
                if (tempPortfolio.sharpeRatio > PortfolioList[threadIndex][PortfolioList[threadIndex].Count() - 1].sharpeRatio || PortfolioList[threadIndex].Count() < maxPortfolios)
                {
                    if(riskLevelCalculation)
                    {
                        if(tempPortfolio.stdDeviation <= maxRiskLevel)
                        {
                            PortfolioList[threadIndex].Add(tempPortfolio);
                        }
                    }
                    else if(returnLevelCalculation)
                    {
                        if(tempPortfolio.averageReturn >= requiredReturn)
                        {
                            PortfolioList[threadIndex].Add(tempPortfolio);
                        }

                    }
                    else
                    {
                        PortfolioList[threadIndex].Add(tempPortfolio);
                    }

                    

                    PortfolioList[threadIndex].Sort((s1, s2) => s2.sharpeRatio.CompareTo(s1.sharpeRatio));

                    if (PortfolioList[threadIndex].Count() > maxPortfolios)
                    {
                        /*
                        this.Dispatcher.Invoke(() =>
                        {
                            addStatusLine("Removing portfolio, Shrp Ratio: " + PortfolioList[threadIndex][maxPortfolios].sharpeRatio);
                        });
                        */
                        PortfolioList[threadIndex].RemoveAt(maxPortfolios);
                    }
                }
            }
            

        }
        
        private float coVarVarianceRecur(int layer, int targetDepth, int startIndex, float[] weightList, int[] productArray, float coVarVariance, int[] array)
        {
            for (int i = startIndex; i <= productArray.GetUpperBound(0); i++)
            {
                array[layer] = i;
                if (layer < targetDepth - 1)
                {
                    coVarVarianceRecur(layer + 1, targetDepth, i + 1, weightList, productArray, coVarVariance, array);
                }
                else if (layer == targetDepth - 1)
                {
                    coVarVariance += 2 * (weightList[array[0]] * weightList[array[1]] * CovarianceMatrix[productArray[array[0]], productArray[array[1]]]);
                }

            }
            return coVarVariance;

        }

        private void Btn_GeneratePortfolios_Click(object sender, RoutedEventArgs e)
        {
            if (!calculatedWeights)
            {
                addStatusLine("Calculate Weights first!");
                return;
            }
            if(!validThreadCount())
            {
                return;
            }
            /*Removed due to overflows
            if(!calculatedTrimProdList)
            {
                if (MessageBox.Show("Test entire search space? It may be better to trim first", "No Trim", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.No)
                {
                    //no
                    return;
                }
            }
            */
            //int threadCount = int.Parse(txt_PortfolioThreads.Text);
            cancelCalcs = false;
            Btn_CancelCalculation.Visibility = Visibility.Visible;
            int prods = (int)Sld_NumProducts.Value;
            skipped = new int[prods];
            returnLevelCalculation = false;
            riskLevelCalculation = false;
            maxPortfolios = (int)Sld_NumberPortfolios.Value;
            riskFreeRate = (float.Parse(txt_RiskFreeRate.Text) / 100F) / 12F;
            FinalPortfolioList = new List<portfolio>();
            InterimPortfolioList = new List<portfolio>();
            PortfolioList = new List<List<portfolio>>();

            startPortfolioCalc(/*threadCount, */prods);




        }

        public void startPortfolioCalc(/*int threadCount, */int prods)
        {
            //Removed due to overflows autoCalcPortfolios = false;
            int totalProds = CovarianceMatrix.GetUpperBound(0);
            portfoliothreadCount = 0;
            /*
            if (threadCount > totalProds)
            {
                portfoliothreadCount = totalProds;
            }
            else
            {
                portfoliothreadCount = threadCount;
            }
            */
            /* Removed due to overflows
            if (!calculatedTrimProdList)
            {
                trimmedProdList = new List<int[]>();
                numChecked = 0;
                Thread tempThread = new Thread(() => trimPortfolios(prods, false, threadCount));
                tempThread.IsBackground = true;
                tempThread.Start();
                autoCalcPortfolios = true;
                return;
            }
            */

            portfolioExportFolder = setupExportFolder(EXPORT_FOLDER, "Portfolios");

            estimatedPortfolioTests = nCr(CovarianceMatrix.GetUpperBound(0) + 1, prods) * WeightingMatrix.Count();
            //Removed due to overflows  estimatedPortfolioTests = trimmedProdList.Count() * WeightingMatrix.Count();
            this.Dispatcher.Invoke(() =>
            {
                txt_TotalPortfoliosRemaining.Text = estimatedPortfolioTests.ToString("#,###");
            });
            calcStartTime = DateTime.Now;
            PortfolioList = new List<List<portfolio>>();


            numChecked = 0;
            
            
            /*
            int prodsPerThread = (int)Math.Ceiling((double)totalProds / (double)portfoliothreadCount);
            int threadIndex = 0;
            for (int i = 0; i < portfoliothreadCount; i++)
            {
                List<portfolio> newList = new List<portfolio>();
                PortfolioList.Add(newList);
            }
            */

            
            for (int i = 0; i < totalProds; i++)
            {
                List<portfolio> newList = new List<portfolio>();
                PortfolioList.Add(newList);
            }

            /*

            
            for (int i = 0; i < portfoliothreadCount; i++)
            {
                int startProdIndex = i * prodsPerThread;
                int endProdIndex = (i + 1) * prodsPerThread;
                if (endProdIndex > totalProds)
                {
                    endProdIndex = totalProds;
                }
                int tempThreadIndex = threadIndex;
                Thread tempThread = new Thread(() => generatePortfolios(prods, startProdIndex, endProdIndex - 1, tempThreadIndex));
                tempThread.IsBackground = true;
                tempThread.Start();
                runningThreads++;
                threadIndex++;

            }
            
            */


            //Massively Multi-Threader (MMT)

            int rowCount = (int)Math.Ceiling(Math.Sqrt((totalProds+1)));
            int colCount = rowCount;
            int elipseSize = ((int)Canvas_PortThreads.Width / rowCount) - 2;


            Thread[] tempThreadArr = new Thread[totalProds];
            Thread tempThread;
            int threadIndex = 0;
            for (int i = 0; i < totalProds; i++)
            {
                Ellipse ThreadStatusElipse = new Ellipse();
                int elipseXIndex = (int)(threadIndex % (double)rowCount);
                int elipseYIndex = (int)Math.Floor(threadIndex / (double)rowCount);
                int elipseX = (elipseXIndex * (elipseSize+2)) + 2;
                int elipseY = (elipseYIndex * (elipseSize+2)) + 2;

                ThreadStatusElipse.Width = elipseSize;
                ThreadStatusElipse.Height = elipseSize;
                ThreadStatusElipse.Fill = GREEN_BRUSH;
                Canvas.SetLeft(ThreadStatusElipse, elipseX);
                Canvas.SetTop(ThreadStatusElipse, elipseY);
                Canvas_PortThreads.Children.Add(ThreadStatusElipse);
                

                int tempThreadIndex = threadIndex;
                int startIndex = i;
                int endIndex = i;
                int totThreads = totalProds+1;
                tempThread = new Thread(() => generatePortfolios(prods, startIndex, endIndex, tempThreadIndex, totThreads, ThreadStatusElipse));
                tempThread.IsBackground = true;
                if(tempThreadIndex < 8)
                {
                    tempThread.Priority = ThreadPriority.Normal;
                }
                else
                {
                    tempThread.Priority = ThreadPriority.BelowNormal;
                }
                tempThreadArr[i] = tempThread;          
                runningThreads++;
                threadIndex++;

            }


            tempThread = new Thread(() => startThreads(tempThreadArr));
            tempThread.IsBackground = true;
            tempThread.Priority = ThreadPriority.BelowNormal;
            tempThread.Start();

            tempThread = new Thread(() => monitorGenerationProgress());
            tempThread.IsBackground = true;
            tempThread.Priority = ThreadPriority.BelowNormal;
            tempThread.Start();


            



            /*
            do
            {
                Thread.Sleep(1000);
            } while (runningThreads > 0);
            */


        }

        public void startThreads(Thread[] inArray)
        {
            for (int i = 0; i < inArray.Count(); i++)
            {
                Thread.Sleep(100);
                inArray[i].Start();
            }
        }

        public void monitorGenerationProgress()
        {
            do
            {
                Thread.Sleep(1000);
                DateTime tempTime = DateTime.Now;
                timetaken = tempTime - calcStartTime;
                double percentComp = (double)numChecked / (double)(numChecked + estimatedPortfolioTests);
                if (percentComp > 0)
                {
                    double millisecondsTotal = timetaken.TotalMilliseconds / percentComp;
                    DateTime temp = DateTime.Now;
                    if (millisecondsTotal > int.MaxValue)
                    {
                        temp = calcStartTime.AddMilliseconds(int.MaxValue);
                    }
                    else
                    {
                        temp = calcStartTime.AddMilliseconds(millisecondsTotal);
                    }

                    remainTime = temp - tempTime;

                }

                this.Dispatcher.Invoke(() =>
                {
                    txt_TotalPortfolios.Text = numChecked.ToString("#,###");
                    txt_TotalPortfoliosRemaining.Text = estimatedPortfolioTests.ToString("#,###");
                    txt_TimeRemaining.Text = remainTime.ToString(@"dd\:hh\:mm\:ss");
                    txt_TimeElapsed.Text = timetaken.ToString(@"dd\:hh\:mm\:ss");
                });

            } while (runningThreads > 0 && !cancelCalcs);

            this.Dispatcher.Invoke(() =>
            {
                addStatusLine("Generating Portfolio List Completed");
                Btn_CancelCalculation.Visibility = Visibility.Hidden;
                for(int i = Canvas_PortThreads.Children.Count-1; i >= 0; i--)
                {
                    Canvas_PortThreads.Children.RemoveAt(i);
                }
                
            });


        }

        public void finalisePortfolios()
        {
            FinalPortfolioList = new List<portfolio>();

            for (int i = 0; i < PortfolioList.Count(); i++)
            {
                for (int j = 0; j < PortfolioList[i].Count(); j++)
                {
                    FinalPortfolioList.Add(PortfolioList[i][j]);
                }

            }

            FinalPortfolioList.Sort((s1, s2) => s2.sharpeRatio.CompareTo(s1.sharpeRatio));

            if (FinalPortfolioList.Count() > maxPortfolios)
            {
                for (int i = FinalPortfolioList.Count() - 1; i >= maxPortfolios; i--)
                {
                    FinalPortfolioList.RemoveAt(i);
                }

            }

            this.Dispatcher.Invoke(() =>
            {
                addStatusLine("Portfolio List Generation Complete");

                txt_TotalPortfolios.Text = numChecked.ToString("#,###");
                txt_TotalPortfoliosRemaining.Text = "0";

                addStatusLine("Top Portfolios:");
                for (int i = 0; i < maxPortfolios; i++)
                {
                    addStatusLine("  Portfolio: " + i.ToString() + " Sharpe Ratio: " + Math.Round(FinalPortfolioList[i].sharpeRatio, 2).ToString() + " Return: " + Math.Round(FinalPortfolioList[i].averageReturn * 100, 2).ToString() + " Std Dev: " + Math.Round(FinalPortfolioList[i].stdDeviation * 100, 2).ToString());

                    for (int j = 0; j < FinalPortfolioList[i].products.Count(); j++)
                    {
                        addStatusLine("    Product:" + FinalPortfolioList[i].products[j].ToString() + " Weight: " + FinalPortfolioList[i].weightings[j].ToString());
                    }
                }
                TimeSpan totalTime = DateTime.Now - calcStartTime;

                addStatusLine("Total time taken: " + totalTime.ToString(@"hh\:mm\:ss"));

                for (int i = 0; i < skipped.Count(); i++)
                {
                    addStatusLine("Layer: " + i.ToString() + " Skipped: " + skipped[i].ToString());
                }


            });

            exportPortfolioList(FinalPortfolioList, portfolioExportFolder + "\\" + PORTFOLIO_FILE);

            
        }

        public static long nCr(int n, int r)
        {
            // naive: return Factorial(n) / (Factorial(r) * Factorial(n - r));
            return nPr(n, r) / Factorial(r);
        }

        public static long nPr(int n, int r)
        {
            // naive: return Factorial(n) / Factorial(n - r);
            return FactorialDivision(n, n - r);
        }

        private static long FactorialDivision(int topFactorial, int divisorFactorial)
        {
            long result = 1;
            for (int i = topFactorial; i > divisorFactorial; i--)
                result *= i;
            return result;
        }

        private static long Factorial(int i)
        {
            if (i <= 1)
                return 1;
            return i * Factorial(i - 1);
        }

        private void Sld_NumProducts_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            calculatedWeights = false;
        }

        private void Sld_PercentageStep_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            calculatedWeights = false;
        }

        private void exportPortfolioList(List<portfolio> inPortfolios, string outFile)
        {
            string[] temp = new string[inPortfolios.Count];
            for (int i = 0; i < inPortfolios.Count; i++)
            {
                for (int j = 0; j < inPortfolios[i].products.Count();j++)
                {
                    temp[i] += inPortfolios[i].products[j] + "*" + inPortfolios[i].weightings[j].ToString() + "  |  ";
                }
                temp[i] += ",";
                
                temp[i] += Math.Round(inPortfolios[i].averageReturn*100F,4).ToString() + ",";
                temp[i] += Math.Round(inPortfolios[i].stdDeviation*100F,4).ToString() + ",";
                temp[i] += Math.Round(inPortfolios[i].sharpeRatio,4).ToString() + ",";
            }

            File.WriteAllLines(outFile, temp);
        }

        private void Btn_LoadProductList_Click(object sender, RoutedEventArgs e)
        {
            loadedProducts = false;
            calculatedReturns = false;
            calculatedCovariances = false;
            calculatedWeights = false;
            Thread tempThread = new Thread(() => loadProductsAndCalcReturns());
            tempThread.IsBackground = true;
            tempThread.Start();
        }

        private void loadProductsAndCalcReturns()
        {
            loadProducts();
            calcReturns();
        }

        private void loadProducts()
        {
            string filename = pickFile(EXPORT_FOLDER, "Product Lists|*.ProdList");

            if(filename == null)
            {
                this.Dispatcher.Invoke(() =>
                {
                    addStatusLine("No file selected");
                    return;
                });
            }
            else
            {
                this.Dispatcher.Invoke(() =>
                {
                    lbl_ProductListFile.Text = filename;
                });
            }

            this.Dispatcher.Invoke(() =>
            {
                addStatusLine("Loading product list");
            });

            products = new List<product>();

            string[] prodList = File.ReadAllLines(filename);            

            for (int i = 0; i < prodList.Count(); i++)
            {
                product temp = new product();
                string[] tempArr = prodList[i].Split(',');
                temp.productID = tempArr[0];
                temp.productName = tempArr[1];
                temp.adminPercent = float.Parse(tempArr[2]);
                products.Add(temp);
            }

            this.Dispatcher.Invoke(() =>
            {
                addStatusLine("Load complete");
            });

            if(!allPricesHaveProducts())
            {
                this.Dispatcher.Invoke(() =>
                {
                    addStatusLine("Missing Products");
                });
                if (MessageBox.Show("Not all prices have a matching product. This can lead to issues with return calculations as admin % will not be included! Do you wish to continue anyway?", "WARNING PRODUCTS MISSING", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    //yes
                    loadedProducts = true;
                    this.Dispatcher.Invoke(() =>
                    {
                        addStatusLine("Continuing with missing products");
                    });
                }
                else
                {
                    loadedProducts = false;
                    products = null;
                    this.Dispatcher.Invoke(() =>
                    {
                        addStatusLine("Product Load Failed, missing products");
                    });
                }
            }

            
            
        }

        private bool allPricesHaveProducts()
        {

            bool output = true;
            for(int i = 0; i < productPrices.Count();i++)
            {
                bool foundMatch = false;
                for (int j = 0; j < products.Count(); j++)
                {
                    if(productPrices[i][0].productID == products[j].productID)
                    {
                        foundMatch = true;
                        break;
                    }
                }

                if(!foundMatch)
                {
                    output = false;
                    break;  
                }
            }

            return output;
        }

        private float getMonthlyAdmin(string prodID)
        {
            float output = 0F;

            for (int i = 0; i < products.Count(); i++)
            {
                if (products[i].productID == prodID)
                {
                    output = products[i].adminPercent / 12F;
                    break;
                }
            }

            return output;
        }

        private bool validThreadCount()
        {
            if (txt_ProductStartID.Text.Length == 0)
            {
                addStatusLine("Ensure that thread count is between 1 and 8");
                return false;
            }


            /*if (!(txt_PortfolioThreads.Text.All(char.IsDigit)))
            {
                addStatusLine("Ensure that thread count is between 1 and 8");
                return false;
            }
            
            int tempThreadCount = int.Parse(txt_PortfolioThreads.Text);
            

            if (tempThreadCount < 1 || tempThreadCount > 8)
            {
                addStatusLine("Ensure that thread count is between 1 and 8");
                return false;
            }
            */

            return true;


        }

        private void txt_TimeRemaining_Copy_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void txt_TimeRemaining_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
        /* Removed due to overflows
        private void productTrimRecur(int layer, int targetDepth, int[] array, int startIndex, bool trimPosOnly)
        {
            for (int i = startIndex; i <= CovarianceMatrix.GetUpperBound(0); i++)
            {
                array[layer] = i;

                if (layer < targetDepth - 1)
                {
                    productTrimRecur(layer + 1, targetDepth, array, i + 1, trimPosOnly);
                }
                else if (layer == targetDepth - 1)
                {
                   if(checkTrimPortfolio(array) || !trimPosOnly)
                    {
                        int[] temp = new int[array.Count()];
                        for (int j = 0; j < array.Count();j++)
                        {
                            temp[j] = array[j];
                        }

                        trimmedProdList.Add(temp);
                    }
                    numChecked++;

                    if(numChecked % 100000 == 0)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            txt_TotalTestedPortfolios.Text = numChecked.ToString("#,###");
                            txt_TotalTrimPortfolios.Text = trimmedProdList.Count().ToString("#,###");
                        });
                    }
                }
            }

        }
        */

        /* Removed due to overflows
        private bool checkTrimPortfolio(int[] productArray)
        {
            
            float portfolioReturn = 0F;

            for (int i = 0; i < productArray.Count(); i++)
            {
                float prodReturn = 0F;
                for (int j = 0; j < realReturns.GetUpperBound(1) + 1; j++)
                {
                    prodReturn += realReturns[productArray[i], j];
                }
                prodReturn = prodReturn / (realReturns.GetUpperBound(1) + 1);
                portfolioReturn += (prodReturn * (1F / productArray.Count()));
            }
            portfolioReturn -= riskFreeRate;

            return portfolioReturn > 0;
            
        }
        */

        private void Btn_TrimPortfolios_Click(object sender, RoutedEventArgs e)
        {
            /* Removed due to overflows
            int prods = (int)Sld_NumProducts.Value;
            int threadCount = int.Parse(txt_PortfolioThreads.Text);
            maxPortfolios = (int)Sld_NumberPortfolios.Value;
            riskFreeRate = (float.Parse(txt_RiskFreeRate.Text) / 100F) / 12F;
            trimmedProdList = new List<int[]>();
            numChecked = 0;
            Thread tempThread = new Thread(() => trimPortfolios(prods,true, threadCount));
            tempThread.IsBackground = true;
            tempThread.Start();
            */
        }

        /* Removed due to overflows

        private void trimPortfolios(int prods, bool trimPosOnly, int threadCount)
        {
            int[] tempArr = new int[prods];
            productTrimRecur(0, prods, tempArr, 0, trimPosOnly);
            this.Dispatcher.Invoke(() =>
            {
                txt_TotalTestedPortfolios.Text = numChecked.ToString("#,###");
                txt_TotalTrimPortfolios.Text = trimmedProdList.Count().ToString("#,###");
            });
            calculatedTrimProdList = true;

            if(autoCalcPortfolios)
            {
                startPortfolioCalc(threadCount, prods);
            }
        }

        */

        private void Btn_UnTrimPortfolios_Click(object sender, RoutedEventArgs e)
        {
            /* Removed due to overflows
            calculatedTrimProdList = false;
            trimmedProdList = null;
            txt_TotalTestedPortfolios.Text = "0";
            txt_TotalTrimPortfolios.Text = "0";
            */
        }

        private void Btn_CalculateAverageReturns_Click(object sender, RoutedEventArgs e)
        {
            if (!calculatedReturns)
            {
                addStatusLine("Calculate Returns first!");
                return;
            }

            if (Dte_StartDate.SelectedDate == null || Dte_EndDate.SelectedDate == null)
            {
                addStatusLine("Ensure that both a start and end date are selected!");
                return;
            }

            startDate = Dte_StartDate.SelectedDate.Value;
            endDate = Dte_EndDate.SelectedDate.Value;

            Thread tempThread = new Thread(() => calcAvgReturnsAndCoVars());
            tempThread.IsBackground = true;
            tempThread.Start();

        }

        private void calcAvgReturnsAndCoVars()
        {
            calcAvgReturns();
            calcCoVar(startDate, endDate);
        }

        private void calcAvgReturns()
        {
            this.Dispatcher.Invoke(() =>
            {
                addStatusLine("Calculating Average Returns");
            });

            calcRealReturns(productMonthlyReturns, startDate, endDate);

            calcAverageReturns();

            calcStdDeviations();

            calculatedAverageReturns = true;

            this.Dispatcher.Invoke(() =>
            {
                addStatusLine("Average Returns Calculated");
            });
        }

        private void calcStdDeviations()
        {

            standardDevs = new float[realReturns.GetUpperBound(0) + 1];

            for (int i = 0; i < realReturns.GetUpperBound(0) + 1; i++)
            {
                standardDevs[i] = 0;
                for (int j = 0; j < realReturns.GetUpperBound(1) + 1; j++)
                {
                    standardDevs[i] += (float)Math.Pow(realReturns[i, j] - averageReturns[i], 2);
                }
                standardDevs[i] = (float)Math.Sqrt(standardDevs[i] / (realReturns.GetUpperBound(1) + 1));
            }

        }

        private void Btn_GenerateSingleWeighting_Click(object sender, RoutedEventArgs e)
        {
            if (!calculatedCovariances)
            {
                addStatusLine("Calculate Co-Variances first!");
                return;
            }

            int prods = (int)Sld_NumProducts.Value;

            WeightingMatrix = new List<float[]>();

            float[] tempWeights = new float[prods];

            for(int i = 0; i < prods; i++)
            {
                tempWeights[i] = (1F / (float)prods);
            }
            
            WeightingMatrix.Add(tempWeights);

            txt_TotalWeights.Text = WeightingMatrix.Count().ToString("#,###");

            calculatedWeights = true;

            addStatusLine("Weights calculated");


        }

        private void Btn_GeneratePortfoliosQuick_Click(object sender, RoutedEventArgs e)
        {
            if (!calculatedWeights)
            {
                addStatusLine("Calculate Weights first!");
                return;
            }
            if (!validThreadCount())
            {
                return;
            }

            //int threadCount = int.Parse(txt_PortfolioThreads.Text);
            cancelCalcs = false;
            Btn_CancelCalculation.Visibility = Visibility.Visible;
            int prods = (int)Sld_NumProducts.Value;
            int interimProds = 3;
            skipped = new int[prods];
            maxPortfolios = MAX_INTERIM_FOLIOS;
            riskFreeRate = (float.Parse(txt_RiskFreeRate.Text) / 100F) / 12F;
            quickCalcPortfolios = true;
            returnLevelCalculation = false;
            riskLevelCalculation = false;

            FinalPortfolioList = new List<portfolio>();
            InterimPortfolioList = new List<portfolio>();
            PortfolioList = new List<List<portfolio>>();

            Thread tempThread = new Thread(() => startPortfolioCalcQuick(/*threadCount,*/ interimProds, prods));
            tempThread.IsBackground = true;
            tempThread.Start();
            

        }

        private void startPortfolioCalcQuick(/*int threadCount, */int interimProds, int prods)
        {
            
            int totalProds = CovarianceMatrix.GetUpperBound(0);
            numChecked = 0;
            estimatedPortfolioTests = 0;
            /*
            portfoliothreadCount = 0;
            
            if (threadCount > totalProds)
            {
                portfoliothreadCount = totalProds;
            }
            else
            {
                portfoliothreadCount = threadCount;
            }
            */

            portfolioExportFolder = setupExportFolder(EXPORT_FOLDER, "Portfolios");

            estimatedPortfolioTests = nCr(CovarianceMatrix.GetUpperBound(0) + 1, interimProds) * WeightingMatrix.Count();

            
            this.Dispatcher.Invoke(() =>
            {
                txt_TotalPortfoliosRemaining.Text = estimatedPortfolioTests.ToString("#,###");
            });
            

           


            /*
            int prodsPerThread = (int)Math.Ceiling((double)totalProds / (double)portfoliothreadCount);
            
            for (int i = 0; i < portfoliothreadCount; i++)
            {
                List<portfolio> newList = new List<portfolio>();
                PortfolioList.Add(newList);
            }
            
            for (int i = 0; i < portfoliothreadCount; i++)
            {
                int startProdIndex = i * prodsPerThread;
                int endProdIndex = (i + 1) * prodsPerThread;
                if (endProdIndex > totalProds)
                {
                    endProdIndex = totalProds;
                }
                int tempThreadIndex = threadIndex;
                Thread tempThread = new Thread(() => generatePortfolios(interimProds, startProdIndex, endProdIndex - 1, tempThreadIndex));
                tempThread.IsBackground = true;
                tempThread.Start();
                runningThreads++;
                threadIndex++;

            }
            */


            WeightingMatrix = new List<float[]>();

            float[] tempWeights = new float[interimProds];

            for (int i = 0; i < interimProds; i++)
            {
                tempWeights[i] = (1F / (float)interimProds);
            }

            WeightingMatrix.Add(tempWeights);


            PortfolioList = new List<List<portfolio>>();






            //Multi threaded - but has issues (cosmetic issues only)
            /*
            for (int i = 0; i < totalProds; i++)
            {
                List<portfolio> newList = new List<portfolio>();
                PortfolioList.Add(newList);
            }

            Thread[] tempThreadArr = new Thread[totalProds];
            int threadIndex = 0;
            for (int i = 0; i < totalProds; i++)
            {
                int tempThreadIndex = threadIndex;
                int startIndex = i;
                int endIndex = i;
                Thread tempThread = new Thread(() => generatePortfolios(interimProds, startIndex, endIndex, tempThreadIndex));
                tempThread.IsBackground = true;
                tempThread.Priority = ThreadPriority.BelowNormal;
                tempThreadArr[i] = tempThread;
                runningThreads++;
                threadIndex++;

            }




            for (int i = 0; i < tempThreadArr.Count(); i++)
            {
                tempThreadArr[i].Start();
            }

            do
            {
                this.Dispatcher.Invoke(() =>
                {
                    txt_TotalPortfolios.Text = numChecked.ToString("#,###");
                    txt_TotalPortfoliosRemaining.Text = estimatedPortfolioTests.ToString("#,###");
                });
                Thread.Sleep(1000);

            } while (runningThreads > 0);

            */


            // Single threaded - but fast anyway
            List<portfolio> newList = new List<portfolio>();
            PortfolioList.Add(newList);

            generatePortfolios(interimProds, 0, totalProds, 0,1);

            ///////////////////////



            FinalPortfolioList = new List<portfolio>();

            for (int i = 0; i < PortfolioList.Count(); i++)
            {
                for (int j = 0; j < PortfolioList[i].Count(); j++)
                {

                    if (riskLevelCalculation)
                    {
                        if (PortfolioList[i][j].stdDeviation <= maxRiskLevel)
                        {
                            FinalPortfolioList.Add(PortfolioList[i][j]);
                        }
                    }
                    else if (returnLevelCalculation)
                    {
                        if (PortfolioList[i][j].averageReturn >= requiredReturn)
                        {
                            FinalPortfolioList.Add(PortfolioList[i][j]);
                        }

                    }
                    else
                    {
                        FinalPortfolioList.Add(PortfolioList[i][j]);
                    }

                   
                }

            }

            FinalPortfolioList.Sort((s1, s2) => s2.sharpeRatio.CompareTo(s1.sharpeRatio));

            if (FinalPortfolioList.Count() > maxPortfolios)
            {
                for (int i = FinalPortfolioList.Count() - 1; i >= maxPortfolios; i--)
                {
                    FinalPortfolioList.RemoveAt(i);
                }

            }

            if(FinalPortfolioList.Count() == 0)
            {
                cancelCalcs = true;
                this.Dispatcher.Invoke(() =>
                {
                    addStatusLine("No portfolios available with specified parameters");
                    Btn_CancelCalculation.Visibility = Visibility.Hidden;
                    txt_TotalPortfolios.Text = "0";
                    txt_TotalPortfoliosRemaining.Text = "0";
                });
                return;
            }


            estimatedPortfolioTests = 0;

            estimatedPortfolioTests += (MAX_INTERIM_FOLIOS * (prods - interimProds) * (CovarianceMatrix.GetUpperBound(0) + 1));




            calcStartTime = DateTime.Now;

            for (int i = interimProds; i < prods; i++)
            {
                if (cancelCalcs)
                {
                    return;
                }
                InterimPortfolioList = new List<portfolio>();

                for (int j = 0; j < FinalPortfolioList.Count(); j++)
                {
                    portfolio temp = new portfolio();
                    temp.averageReturn = FinalPortfolioList[j].averageReturn;
                    temp.prodIndexes = FinalPortfolioList[j].prodIndexes;
                    temp.products = FinalPortfolioList[j].products;
                    temp.sharpeRatio = FinalPortfolioList[j].sharpeRatio;
                    temp.stdDeviation = FinalPortfolioList[j].stdDeviation;
                    temp.weightings = FinalPortfolioList[j].weightings;

                    InterimPortfolioList.Add(temp);
                }

                quickGenList(InterimPortfolioList);

            }

            if(cancelCalcs)
            {
                return;
            }


            this.Dispatcher.Invoke(() =>
            {
                addStatusLine("Portfolio List Generation Complete");
                Btn_CancelCalculation.Visibility = Visibility.Hidden;
                txt_TotalPortfolios.Text = numChecked.ToString("#,###");
                txt_TotalPortfoliosRemaining.Text = "0";

                addStatusLine("Top Portfolios:");

                int portsToShow = 5;
                if(portsToShow > FinalPortfolioList.Count())
                {
                    portsToShow = FinalPortfolioList.Count();
                }

                for (int i = 0; i < portsToShow; i++)
                {
                    addStatusLine("  Portfolio: " + i.ToString() + " Sharpe Ratio: " + Math.Round(FinalPortfolioList[i].sharpeRatio, 2).ToString() + " Return: " + Math.Round(FinalPortfolioList[i].averageReturn * 100, 2).ToString() + " Std Dev: " + Math.Round(FinalPortfolioList[i].stdDeviation * 100, 2).ToString());

                    for (int j = 0; j < prods; j++)
                    {
                        addStatusLine("    Product:" + FinalPortfolioList[i].products[j].ToString() + " Weight: " + FinalPortfolioList[i].weightings[j].ToString());
                    }
                }
                TimeSpan totalTime = DateTime.Now - calcStartTime;

                addStatusLine("Total time taken: " + totalTime.ToString(@"hh\:mm\:ss"));

                for (int i = 0; i < skipped.Count(); i++)
                {
                    addStatusLine("Layer: " + i.ToString() + " Skipped: " + skipped[i].ToString());
                }


            });

            exportPortfolioList(FinalPortfolioList, portfolioExportFolder + "\\" + PORTFOLIO_FILE);

        }

        private void quickGenList(List<portfolio> folioList)
        {

            PortfolioList[0] = new List<portfolio>();
            int numProds = folioList[0].products.Count() + 1;
            float[] weights = new float[numProds];

            for(int i = 0; i < numProds; i++)
            {
                weights[i] = 1F / numProds;
            }

            for (int i = 0; i < folioList.Count(); i++)
            {
                if (cancelCalcs)
                {
                    return;
                }
                int[] array = new int[numProds];
                for (int j = 0; j < folioList[i].prodIndexes.Count(); j++)
                {
                    array[j] = folioList[i].prodIndexes[j];
                }

                for (int k = 0; k < CovarianceMatrix.GetUpperBound(0) + 1; k++)
                {
                    if (cancelCalcs)
                    {
                        return;
                    }
                    bool found = false;
                    for(int l = 0; l < numProds-1; l++)
                    {
                        if(array[l] == k)
                        {
                            found = true;
                            break;
                        }
                    }
                    if(found)
                    {
                        numChecked++;
                        continue;
                    }

                    array[numProds - 1] = k;                    
                    
                    checkPortfolio(array, weights, 0);
                    numChecked++;
                    estimatedPortfolioTests--;

                    if (numChecked % 5000 == 0)
                    {
                        DateTime tempTime = DateTime.Now;
                        timetaken = tempTime - calcStartTime;
                        double percentComp = (double)numChecked / (double)(numChecked + estimatedPortfolioTests);
                        if (percentComp > 0)
                        {
                            double millisecondsTotal = timetaken.TotalMilliseconds / percentComp;
                            DateTime temp = DateTime.Now;
                            if (millisecondsTotal > int.MaxValue)
                            {
                                temp = calcStartTime.AddMilliseconds(int.MaxValue);
                            }
                            else
                            {
                                temp = calcStartTime.AddMilliseconds(millisecondsTotal);
                            }

                            remainTime = temp - tempTime;

                        }

                        this.Dispatcher.Invoke(() =>
                        {
                            txt_TotalPortfolios.Text = numChecked.ToString("#,###");
                            txt_TotalPortfoliosRemaining.Text = estimatedPortfolioTests.ToString("#,###");
                            txt_TimeRemaining.Text = remainTime.ToString(@"dd\:hh\:mm\:ss");
                            txt_TimeElapsed.Text = timetaken.ToString(@"dd\:hh\:mm\:ss");
                        });
                    }



                }

            }

            FinalPortfolioList = PortfolioList[0];

            if (FinalPortfolioList.Count() == 0)
            {
                cancelCalcs = true;
                this.Dispatcher.Invoke(() =>
                {
                    addStatusLine("No portfolios available with specified parameters");
                    Btn_CancelCalculation.Visibility = Visibility.Hidden;
                    txt_TotalPortfolios.Text = "0";
                    txt_TotalPortfoliosRemaining.Text = "0";
                });
                return;
            }

        }

       
        private void Btn_FinalisePortfolios_Click(object sender, RoutedEventArgs e)
        {
            finalisePortfolios();
        }

        private void frame_Navigated(object sender, NavigationEventArgs e)
        {
            
        }

        private void Btn_GraphAvgReturns_Click(object sender, RoutedEventArgs e)
        {
            graph AvgReturnGraph = new graph();
            AvgReturnGraph.xMin = 0F;
            AvgReturnGraph.xMax = 100F;
            AvgReturnGraph.yMin = 0F;
            AvgReturnGraph.yMax = 10F;
            AvgReturnGraph.HorizMarkers = 20;
            AvgReturnGraph.VertMarkers = 10;


            drawXAxis(AvgReturnGraph);
            drawYAxis(AvgReturnGraph);
        }

        private void drawXAxis(graph inGraph)
        {
            this.Dispatcher.Invoke(() =>
            {
                Line tempLine = new Line();
                tempLine.Stroke = BLACK_BRUSH;
                tempLine.X1 = GRAPH_AXIS_SEPERATION;
                tempLine.X2 = Canvas_GraphArea.Width - GRAPH_AXIS_SEPERATION;
                tempLine.Y1 = Canvas_GraphArea.Height - GRAPH_AXIS_SEPERATION;
                tempLine.Y2 = Canvas_GraphArea.Height - GRAPH_AXIS_SEPERATION;
                tempLine.HorizontalAlignment = HorizontalAlignment.Left;
                tempLine.VerticalAlignment = VerticalAlignment.Center;
                tempLine.StrokeThickness = 1;
                Canvas_GraphArea.Children.Add(tempLine);

                float sepLength = (float)(Canvas_GraphArea.Width - (2 * GRAPH_AXIS_SEPERATION)) / (inGraph.HorizMarkers);
                float sepVals = (inGraph.xMax - inGraph.xMin) / inGraph.HorizMarkers;

                for(int i = 0; i<=inGraph.HorizMarkers;i++)
                {
                    tempLine = new Line();
                    tempLine.Stroke = BLACK_BRUSH;
                    tempLine.X1 = GRAPH_AXIS_SEPERATION + (i * sepLength);
                    tempLine.X2 = tempLine.X1;
                    tempLine.Y1 = Canvas_GraphArea.Height - GRAPH_AXIS_SEPERATION;
                    tempLine.Y2 = Canvas_GraphArea.Height - (GRAPH_AXIS_SEPERATION*1.2);
                    tempLine.HorizontalAlignment = HorizontalAlignment.Left;
                    tempLine.VerticalAlignment = VerticalAlignment.Center;
                    tempLine.StrokeThickness = 1;
                    Canvas_GraphArea.Children.Add(tempLine);
                    
                    Label tempLbl = new Label();
                    tempLbl.Content = Math.Round((i) * sepVals, 2).ToString();
                    tempLbl.Width = 40;
                    tempLbl.Height = 20;
                    tempLbl.Padding = new System.Windows.Thickness(1, 1, 1, 1);
                    tempLbl.HorizontalContentAlignment = HorizontalAlignment.Center;
                    tempLbl.VerticalContentAlignment = VerticalAlignment.Top;
                    tempLbl.FontSize = 9;
                    Canvas_GraphArea.Children.Add(tempLbl);

                    Canvas.SetLeft(tempLbl, tempLine.X1 - (tempLbl.Width/2));
                    Canvas.SetTop(tempLbl, tempLine.Y1);

                }

            });
        }

        private void drawYAxis(graph inGraph)
        {
            this.Dispatcher.Invoke(() =>
            {
                Line tempLine = new Line();
                tempLine.Stroke = BLACK_BRUSH;
                tempLine.X1 = GRAPH_AXIS_SEPERATION;
                tempLine.X2 = GRAPH_AXIS_SEPERATION;
                tempLine.Y1 = Canvas_GraphArea.Height - GRAPH_AXIS_SEPERATION;
                tempLine.Y2 = GRAPH_AXIS_SEPERATION;
                tempLine.HorizontalAlignment = HorizontalAlignment.Left;
                tempLine.VerticalAlignment = VerticalAlignment.Center;
                tempLine.StrokeThickness = 1;
                Canvas_GraphArea.Children.Add(tempLine);

                float sepLength = (float)(Canvas_GraphArea.Height - (2 * GRAPH_AXIS_SEPERATION)) / (inGraph.VertMarkers);
                float sepVals = (inGraph.yMax - inGraph.yMin) / inGraph.VertMarkers;
                int index = inGraph.VertMarkers;

                for (int i = 0; i <= inGraph.VertMarkers; i++)
                {
                    tempLine = new Line();
                    tempLine.Stroke = BLACK_BRUSH;
                    tempLine.X1 = GRAPH_AXIS_SEPERATION;
                    tempLine.X2 = GRAPH_AXIS_SEPERATION + GRAPH_AXIS_SEPERATION / 5;
                    tempLine.Y1 = GRAPH_AXIS_SEPERATION + (i * sepLength);
                    tempLine.Y2 = GRAPH_AXIS_SEPERATION + (i * sepLength);
                    tempLine.HorizontalAlignment = HorizontalAlignment.Left;
                    tempLine.VerticalAlignment = VerticalAlignment.Center;
                    tempLine.StrokeThickness = 1;
                    Canvas_GraphArea.Children.Add(tempLine);

                    Label tempLbl = new Label();
                    tempLbl.Content = Math.Round((index) * sepVals, 2).ToString();
                    tempLbl.Width = 20;
                    tempLbl.Height = 20;
                    tempLbl.Padding = new System.Windows.Thickness(1, 1, 1, 1);
                    tempLbl.HorizontalContentAlignment = HorizontalAlignment.Right;
                    tempLbl.VerticalContentAlignment = VerticalAlignment.Top;
                    tempLbl.FontSize = 9;
                    Canvas_GraphArea.Children.Add(tempLbl);

                    Canvas.SetLeft(tempLbl, tempLine.X1 - tempLbl.Width-2);
                    Canvas.SetTop(tempLbl, tempLine.Y1 - (tempLbl.Height/2));
                    index--;
                }

            });
        }

        private void Btn_CancelCalculation_Click(object sender, RoutedEventArgs e)
        {
            cancelCalcs = true;
        }

        private void Btn_GenerateRiskPortfoliosQuick_Click(object sender, RoutedEventArgs e)
        {
            if (!calculatedWeights)
            {
                addStatusLine("Calculate Weights first!");
                return;
            }
            if (!validThreadCount())
            {
                return;
            }

            //int threadCount = int.Parse(txt_PortfolioThreads.Text);
            cancelCalcs = false;
            Btn_CancelCalculation.Visibility = Visibility.Visible;
            int prods = (int)Sld_NumProducts.Value;
            int interimProds = 3;
            skipped = new int[prods];
            maxPortfolios = MAX_INTERIM_FOLIOS;
            riskFreeRate = (float.Parse(txt_RiskFreeRate.Text) / 100F) / 12F;
            maxRiskLevel = (float.Parse(txt_RiskRate.Text) / 100F)  / 12F;
            returnLevelCalculation = false;
            riskLevelCalculation = true;
            quickCalcPortfolios = true;

            FinalPortfolioList = new List<portfolio>();
            InterimPortfolioList = new List<portfolio>();
            PortfolioList = new List<List<portfolio>>();

            Thread tempThread = new Thread(() => startPortfolioCalcQuick(/*threadCount,*/ interimProds, prods));
            tempThread.IsBackground = true;
            tempThread.Start();
        }

        private void Btn_GenerateReturnPortfoliosQuick_Click(object sender, RoutedEventArgs e)
        {
            if (!calculatedWeights)
            {
                addStatusLine("Calculate Weights first!");
                return;
            }
            if (!validThreadCount())
            {
                return;
            }

            //int threadCount = int.Parse(txt_PortfolioThreads.Text);
            cancelCalcs = false;
            Btn_CancelCalculation.Visibility = Visibility.Visible;
            int prods = (int)Sld_NumProducts.Value;
            int interimProds = 3;
            skipped = new int[prods];
            maxPortfolios = MAX_INTERIM_FOLIOS;
            requiredReturn = (float.Parse(txt_ReturnRate.Text) / 100F) / 12F;
            returnLevelCalculation = true;
            riskLevelCalculation = false;
            quickCalcPortfolios = true;

            FinalPortfolioList = new List<portfolio>();
            InterimPortfolioList = new List<portfolio>();
            PortfolioList = new List<List<portfolio>>();

            Thread tempThread = new Thread(() => startPortfolioCalcQuick(/*threadCount,*/ interimProds, prods));
            tempThread.IsBackground = true;
            tempThread.Start();
        }
    }
}
