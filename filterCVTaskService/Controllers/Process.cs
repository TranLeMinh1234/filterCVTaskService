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
        private string url = "https://my-cv-service.herokuapp.com/";
        public string id { get; set; }
        public List<IFormFileCustom> files { get; set; }
        public string keyWord { get; set; }
        public bool enable_notification { get; set; }
        public int file_count { get; set; }

        public Thread thread { get; set; }

        public List<CV> listCV = new List<CV>();
        public string[] listKeyWord;

        public Process(string id, List<IFormFileCustom> files, int file_count,
             string keyWord, bool enable_notification)
        {
            this.id = id;
            this.files = files;
            this.file_count = file_count;
            this.keyWord = keyWord;
            this.enable_notification = enable_notification;
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

            //step 1 - khởi tạo danh sách cv
            //initCVFromInput();

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

                listCV.Add(cv);
            }

            //step 2 - khởi tạo danh sách tiêu chí
            //initCriteria();
            listKeyWord = keyWord.Split(",");

            //step 3 - lấy thông tin cv
            await this.GetTextInfoCV(httpClient);

            //step 4 - tìm số từ 
            await this.FindMatchedInfo(httpClient);

            //step 5,6,7
            //filter();
            //step 8
            //sortCVByNumberOfMatchedCriteria();
            //final
            //returnResutlt();
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
                         await httpClient.PostAsync("https://my-keyword-finder.herokuapp.com/keyword/finder", postData))
                {
                    var jsonString = await message.Content.ReadAsStringAsync();
                    var jsonObject = JsonConvert.DeserializeObject<KeyWordApiResult>(jsonString);
                    cv.numberOfMatched = Convert.ToInt32(jsonObject.numberOfMatchedKeyword);
                    cv.listMatched = jsonObject.listKeyword;
                }
            }
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

            public string[] listMatched { get; set; }
            public IFormFile file { get; set; }
            public byte[] byteArray { get; set; }
        }
    }
}
