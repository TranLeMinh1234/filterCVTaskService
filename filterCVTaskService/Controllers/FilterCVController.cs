using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace filterCVTaskService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilterCVController : ControllerBase
    {
        private Dictionary<string, Process> _manager = new Dictionary<string, Process>();

        [HttpPost]
        public IActionResult Start([FromForm] List<IFormFile> files, [FromForm] int file_count,
            [FromForm] string keyWord, [FromForm] bool enable_notification)
        {
            List<IFormFileCustom> listIFormFileCustoms = new List<IFormFileCustom>();
            // tiền xử lý stream cho file
            foreach (IFormFile file in files)
            {
                var iFormFileCustom = new IFormFileCustom();
                iFormFileCustom.file = file;
                using (var ms = new MemoryStream())
                {
                    file.CopyTo(ms);
                    iFormFileCustom.byteArray = ms.ToArray();
                }
                listIFormFileCustoms.Add(iFormFileCustom);
            }

            //Khởi tạo proccess-id định danh
            string newProcessId = Guid.NewGuid().ToString();
            Process newProcess = new Process(newProcessId, listIFormFileCustoms, file_count, keyWord, enable_notification);

            _manager.Add(newProcessId, newProcess);

            //bắt đầu process
            newProcess.Start();

            return Ok();
        }

        public class IFormFileCustom
        {
            public IFormFile file { get; set; }
            public byte[] byteArray { get; set; }
        }
    }
}
