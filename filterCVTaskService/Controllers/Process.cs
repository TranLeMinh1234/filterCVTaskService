using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static filterCVTaskService.Controllers.FilterCVController;

namespace filterCVTaskService.Controllers
{
    public class Process
    {
        private string url = "https://my-gatewayy.herokuapp.com/";

        public string urlUpdateProcess { get; set; }
        public string id { get; set; }
        public List<IFormFileCustom> files { get; set; }
        public string keyWord { get; set; }
        public bool enable_notification { get; set; }
        public int file_count { get; set; }

        public Thread thread { get; set; }

        public List<CV> listCV = new List<CV>();
        public string[] listKeyWord;

        private Dictionary<string, List<dynamic>> resultFilter = new Dictionary<string, List<dynamic>>();
        private List<dynamic> suitableList = new List<dynamic>();
        private List<dynamic> unsuitableList = new List<dynamic>();

        public Process(string id, List<IFormFileCustom> files, int file_count,
             string keyWord, bool enable_notification, string urlUpdateProcess)
        {
            this.id = id;
            this.files = files;
            this.file_count = file_count;
            this.keyWord = keyWord;
            this.enable_notification = enable_notification;
            this.urlUpdateProcess = urlUpdateProcess;

            this.thread = new Thread(async () =>
            {
                try
                {
                    await this.ExecuteAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            });
        }

        public void Start()
        {
            thread.Start();
        }

        public async Task ExecuteAsync()
        {
            var httpClient = new HttpClient();
            int step = 0;
            string textUpdateProcess = "";

            //step 1 - khởi tạo danh sách cv
            //initCVFromInput();
            step++;
            textUpdateProcess = this.id + "|" + step + "|" + "Khởi tạo danh sách CV";
            await this.UpdateProcess(httpClient, textUpdateProcess);

            foreach (IFormFileCustom iFormFileCustom in this.files)
            {
                CV cv = new CV();
                cv.id = "";
                cv.email = "";
                cv.file = iFormFileCustom.file;
                cv.text = "";
                cv.numberOfMatched = 0;
                cv.byteArray = iFormFileCustom.byteArray;
                cv.listMatched = new string[0];
                cv.matchPercent = 0;

                listCV.Add(cv);
            }

            Thread.Sleep(1000);

            step++;
            textUpdateProcess = this.id + "|" + step + "|" + "Khởi tạo danh sách KeyWord";
            await this.UpdateProcess(httpClient, textUpdateProcess);
            //step 2 - khởi tạo danh sách tiêu chí
            //initCriteria();
            listKeyWord = keyWord.Split(",");

            Thread.Sleep(1000);

            step++;
            textUpdateProcess = this.id + "|" + step + "|" + "Lấy text, thông tin CV";
            await this.UpdateProcess(httpClient, textUpdateProcess);
            //step 3 - lấy thông tin cv
            await this.GetTextInfoCV(httpClient);

            Thread.Sleep(1000);

            step++;
            textUpdateProcess = this.id + "|" + step + "|" + "Tìm kiếm số lượng từ khóa xuất hiện trên CV";
            await this.UpdateProcess(httpClient, textUpdateProcess);
            //step 4 - tìm số từ 
            await this.FindMatchedInfo(httpClient);

            Thread.Sleep(1000);

            step++;
            textUpdateProcess = this.id + "|" + step + "|" + "Lọc CV";
            await this.UpdateProcess(httpClient, textUpdateProcess);
            //step 5,6,7 - lọc CV
            await this.Filter(httpClient);

            Thread.Sleep(1000);

            //step thêm nếu có thiết lập gửi email
            if (enable_notification)
            {
                step++;
                textUpdateProcess = this.id + "|" + step + "|" + "Gửi email thông báo từ chối CV";
                await this.UpdateProcess(httpClient, textUpdateProcess);
                this.SendEmail(httpClient);
            }

            Thread.Sleep(1000);

            step++;
            textUpdateProcess = this.id + "|" + step + "|" + "Sắp xếp CV theo mức độ phù hợp giảm dần";
            await this.UpdateProcess(httpClient, textUpdateProcess);
            //step 8 - sort lại
            this.SortCVByNumberOfMatchedCriteria();

            //final
            resultFilter.Add("suitableList",suitableList);
            resultFilter.Add("unsuitableList", unsuitableList);

            string jsonData = JsonConvert.SerializeObject(resultFilter);

            step++;
            textUpdateProcess = this.id + "|" + "final" + "|" + "Hoàn thành dịch vụ"+"|"+jsonData;
            await this.UpdateProcess(httpClient, textUpdateProcess);

        }

        private void SortCVByNumberOfMatchedCriteria()
        {
            suitableList.Sort((x, y) => x.numberOfMatched > y.numberOfMatched);
        }

        private async Task GetTextInfoCV(HttpClient httpClient)
        {
            foreach (CV cv in listCV)
            {
                using (var content =
                    new MultipartFormDataContent("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture)))
                {
                    content.Add(new StreamContent(new MemoryStream(cv.byteArray)), "file", cv.file.FileName);

                    using (
                        var message =
                            await httpClient.PostAsync(url + "cv/upload", content))
                    {
                        var jsonString = await message.Content.ReadAsStringAsync();
                        dynamic jsonObject = JsonConvert.DeserializeObject(jsonString);
                        cv.id = jsonObject.id;
                    }
                }

                using (
                    var message =
                        await httpClient.GetAsync(url + "cv/getall/"+cv.id))
                {
                    var jsonString = await message.Content.ReadAsStringAsync();
                    dynamic jsonObject = JsonConvert.DeserializeObject(jsonString);
                    cv.text = jsonObject.first;
                    cv.email = jsonObject.second.email;
                }
            }
        }

        private async Task FindMatchedInfo(HttpClient httpClient)
        {
            foreach(CV cv in listCV)
            {
                var objectData = new {
                    text = cv.text,
                    keywords = listKeyWord
                };
                string jsonData = JsonConvert.SerializeObject(objectData);
                var postData = new StringContent(jsonData, Encoding.UTF8, "application/json");
                using (
                     var message =
                         await httpClient.PostAsync(url+"keyword/finder", postData))
                {
                    var jsonString = await message.Content.ReadAsStringAsync();
                    var jsonObject = JsonConvert.DeserializeObject<KeyWordApiResult>(jsonString);
                    cv.numberOfMatched = Convert.ToInt32(jsonObject.numberOfMatchedKeyword);
                    cv.listMatched = jsonObject.listKeyword;
                }
            }
        }

        private async Task SendEmail(HttpClient httpClient)
        {
            foreach (CV cv in listCV)
            {
                if (cv.numberOfMatched == 0)
                {
                    var objectData = new
                    {
                        email = cv.email,
                        subject = "Thông báo xét duyệt CV",
                        content = "Cảm ơn bạn đã gửi CV! Hiện tại bạn chưa phù hợp với tiêu chí của công ty chúng tôi. Hẹn bạn quay lại trong lần tuyển dụng sắp tới!"
                    };
                    string jsonData = JsonConvert.SerializeObject(objectData);
                    var postData = new StringContent(jsonData, Encoding.UTF8, "application/json");
                    using (
                         var message =
                             await httpClient.PostAsync(url + "email/sender", postData))
                    {
                        var jsonString = await message.Content.ReadAsStringAsync();
                        var jsonObject = JsonConvert.DeserializeObject<KeyWordApiResult>(jsonString);
                    }
                }
            }
        }

        private async Task Filter(HttpClient httpClient)
        { 
            foreach(CV cv in listCV)
            {
                cv.matchPercent = (float)100 * ((float)cv.numberOfMatched / listKeyWord.Length);
                if (cv.numberOfMatched > 0)
                {
                    suitableList.Add(new
                    {
                        name = cv.file.FileName,
                        numberOfMatched = cv.numberOfMatched,
                        matchPercent = cv.matchPercent,
                        listMatched = cv.listMatched
                    });
                }
                else
                    unsuitableList.Add(new {
                        name = cv.file.FileName,
                        numberOfMatched = cv.numberOfMatched,
                        matchPercent = cv.matchPercent,
                        listMatched = cv.listMatched
                    });
            }
        }

        private async Task UpdateProcess(HttpClient httpClient, string textInfo)
        {
            var objectData = new
            {
                textInfo = textInfo,
            };
            string jsonData = JsonConvert.SerializeObject(objectData);
            var postData = new StringContent(jsonData, Encoding.UTF8, "application/json");
            using (
                 var message =
                     await httpClient.PostAsync(urlUpdateProcess, postData))
            {}
        }

        public class KeyWordApiResult {
            public int numberOfMatchedKeyword { get; set; }
            public string[] listKeyword { get; set; }
            public object details { get; set; }
        }


        public class CV
        {
            public string id { get; set; }
            public string email { get; set; }
            public string text { get; set; }
            public int numberOfMatched { get; set; }
            public float matchPercent { get; set; }
            public string[] listMatched { get; set; }
            public IFormFile file { get; set; }
            public byte[] byteArray { get; set; }
        }
    }
}
