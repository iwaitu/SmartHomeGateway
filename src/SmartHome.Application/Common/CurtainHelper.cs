﻿using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SmartHome.Application
{
    public class CurtainHelper
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;

        private MqttHelper _mqttHelper;
        private CurtainListener _listener;

        public CurtainHelper(ILogger<CurtainHelper> logger, IConfiguration configuration, IMemoryCache cache)
        {
            _logger = logger;
            _config = configuration;
            _cache = cache;
        }

        public void SetMqttListener(MqttHelper mqttHelper)
        {
            _mqttHelper = mqttHelper;
        }

        public void SetListener(CurtainListener curtainListener)
        {
            _listener = curtainListener;
        }

        public async Task<CurtainStateObject> OnReceiveData(string data)
        {
            //_logger.LogInformation(data);
            Regex reg = new Regex("00 00 00 00 00 06 55 01 (.+) 01 01 (.+)");
            var match = reg.Match(data);
            if(match.Success && match.Groups.Count == 3)
            {
                var id = int.Parse(match.Groups[1].ToString(), System.Globalization.NumberStyles.HexNumber);
                var status = int.Parse(match.Groups[2].ToString(), System.Globalization.NumberStyles.HexNumber);
                var obj = new CurtainStateObject { Id = id, Status = status };
                _cache.Set(obj.Id, obj);
                var message = new MqttApplicationMessageBuilder().WithTopic("Home/Curtain/" + id + "/Status")
                       .WithPayload(JsonConvert.SerializeObject(obj))
                       .WithAtLeastOnceQoS()
                       .Build();
                await _mqttHelper.Publish(message);
                var message1 = new MqttApplicationMessageBuilder().WithTopic("Home/Curtain/" + id + "/State")
                       .WithPayload(obj.Status.ToString())
                       .WithAtLeastOnceQoS()
                       .Build();
                await _mqttHelper.Publish(message);
                return obj;
            }
            return null;
        }

        public async Task<CurtainStateObject> OnReceiveMotorData(string data)
        {
            //_logger.LogInformation(data);
            Regex reg = new Regex("00 00 00 00 00 06 55 01 (.+) 01 01 (.+)");
            var match = reg.Match(data);
            if (match.Success && match.Groups.Count == 3)
            {
                var id = int.Parse(match.Groups[1].ToString(), System.Globalization.NumberStyles.HexNumber);
                var status = int.Parse(match.Groups[2].ToString(), System.Globalization.NumberStyles.HexNumber);
                var obj = new CurtainStateObject { Id = id, Status = status };
                return await Task.FromResult(obj);
            }
            return null;
        }

        /// <summary>
        /// 查询窗帘当前百分比
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<int> GetCurtainStatus(int id)
        {
            var cmd = string.Format("00 00 00 00 00 06 55 01 {0} 01 02 01",id.ToString("X2"));
            var ret = await _listener.SendCommand(cmd);
            if(ret != null)
            {
                return ret.Status;
            }
            return -1;
        }

        /// <summary>
        /// 查询电机状态
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<int> GetCurtainMotorStatus(int id)
        {
            var cmd = string.Format("00 00 00 00 00 06 55 01 {0} 01 05 01", id.ToString("X2"));
            var ret = await _listener.SendMotorCommand(cmd);
            if (ret != null)
            {
                return ret.Status;
            }
            return -1;
        }

        public async Task SetCurtain(int id ,int value)
        {
            var cmd = string.Format("00 00 00 00 00 06 55 01 {0} 03 04 {1}",id.ToString("X2"), value.ToString("X2"));
            await _listener.SendCommand(cmd);
        }

        public async Task Open(int id)
        {
            var cmd = string.Format("00 00 00 00 00 05 55 01 {0} 03 01", id.ToString("X2"));
            await _listener.SendCommand(cmd);
        }

        public async Task Close(int id)
        {
            var cmd = string.Format("00 00 00 00 00 05 55 01 {0} 03 02", id.ToString("X2"));
            await _listener.SendCommand(cmd);
        }

        public async Task Stop(int id)
        {
            var cmd = string.Format("00 00 00 00 00 05 55 01 {0} 03 03", id.ToString("X2"));
            await _listener.SendCommand(cmd);
        }

        public class CurtainStateObject
        {
            public int Id { get; set; }
            public int Status { get; set; }
            public string Command { get; set; }

        }
    }
}
