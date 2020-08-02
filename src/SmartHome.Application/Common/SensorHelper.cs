using Hangfire;
using Microsoft.Extensions.Logging;
using MQTTnet;
using SmartHome.Application.Common;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace SmartHome.Application
{
    public class SensorHelper
    {
        private static readonly HttpClient client = new HttpClient();
        private SensorListener _listener;
        private LightHelper _lightHelper;
        private MqttHelper _mqttHelper;
        private readonly ILogger _logger;
        private INotify _notify;

        public SensorHelper(ILogger<SensorHelper> logger, LightHelper lightHelper,INotify notify)
        {
            _logger = logger;
            _lightHelper = lightHelper;
            _lightHelper.SetSensorHelper(this);
            _notify = notify;
        }

        public void SetListener(SensorListener listener)
        {
            _listener = listener;
        }

        public void SetMqttListener(MqttHelper mqtt)
        {
            _mqttHelper = mqtt;
        }

        public async Task OnReceiveCommand(string Command)
        {
            _logger.LogWarning(Command);
            if(Command == "01 01 0D") //门道感应器
            {
                await OpenDoor();
            }
            else if(Command == "01 00 0D")
            {
                await CloseDoor();
                var message = new MqttApplicationMessageBuilder().WithTopic("Home/Sensor/Door")
                       .WithPayload("0")
                       .WithAtLeastOnceQoS()
                       .Build();
                await _mqttHelper.Publish(message);
            }
            else if (Command == "02 01 0D") //过道探测器报警
            {
                await OpenAisle(SensorType.Aisle);
                
            }
            else if (Command == "02 00 0D") 
            {
                await CloseAisle();
                var message = new MqttApplicationMessageBuilder().WithTopic("Home/Sensor/Aisle")
                       .WithPayload("0")
                       .WithAtLeastOnceQoS()
                       .Build();
                await _mqttHelper.Publish(message);
            }
            else if (Command == "03 01 0D") //烟雾探测器报警
            {
                var message = new MqttApplicationMessageBuilder().WithTopic("Home/Sensor/Smoke")
                       .WithPayload("1")
                       .WithAtLeastOnceQoS()
                       .Build();
                await _mqttHelper.Publish(message);
                _logger.LogWarning("烟雾探测器报警");
                _notify.Send("厨房烟雾");
            }
            else if (Command == "03 00 0D") 
            {
                var message = new MqttApplicationMessageBuilder().WithTopic("Home/Sensor/Smoke")
                       .WithPayload("0")
                       .WithAtLeastOnceQoS()
                       .Build();
                await _mqttHelper.Publish(message);
                _logger.LogWarning("烟雾探测器取消报警");
                _notify.Send("厨房烟雾解除");
            }
            else if (Command == "04 01 0D") //主灯打开成功
            {

            }
            else if (Command == "04 00 0D") //主灯关闭成功
            {

            }
        }

        public void OpenMainLight()
        {
            _listener.PublishCommand("04 01 0D");
        }

        public void CloseMainLight()
        {
            _listener.PublishCommand("04 00 0D");
        }

        /// <summary>
        /// 打开过道灯光
        /// </summary>
        /// <returns></returns>
        public async Task OpenAisle(SensorType type = SensorType.Door)
        {

            switch (_lightHelper.CurrentStateMode)
            {
                case StateMode.Home:
                    await _lightHelper.OpenAisle(80);
                    var jobId = BackgroundJob.Schedule(() => _lightHelper.OpenAisle(50), TimeSpan.FromSeconds(15));
                    break;
                case StateMode.Out:
                    //开始报警
                    await _lightHelper.OpenDoor(100);
                    BackgroundJob.Schedule(() => CallAlert(type), TimeSpan.FromSeconds(15));
                    BackgroundJob.Schedule(() => _lightHelper.OpenAisle(50), TimeSpan.FromSeconds(15));
                    break;
                case StateMode.Read:
                    await _lightHelper.OpenAisle(40);
                    break;
            }
            
        }

        public async Task CloseAisle()
        {
            await _lightHelper.CloseAisle();
        }

        /// <summary>
        /// 打开门道灯光
        /// </summary>
        /// <returns></returns>
        public async Task OpenDoor(SensorType type = SensorType.Door)
        {
            switch (_lightHelper.CurrentStateMode)
            {
                case StateMode.Home:
                    await _lightHelper.OpenDoor(80);
                    var jobId = BackgroundJob.Schedule(() => _lightHelper.OpenDoor(50), TimeSpan.FromSeconds(15));
                    break;
                case StateMode.Out:
                    await _lightHelper.OpenDoor(100);
                    //开始报警
                    //await Task.Run(async () => { await CallAlert(type); });
                    BackgroundJob.Schedule(() => _lightHelper.OpenDoor(50), TimeSpan.FromSeconds(15));
                    BackgroundJob.Schedule(() => CallAlert(type), TimeSpan.FromSeconds(15));
                    break;
                case StateMode.Read:
                    await _lightHelper.OpenDoor(40);
                    break;
            }
            
        }

        public async Task CallAlert(SensorType type)
        {
            if (_lightHelper != null && _mqttHelper != null)
            {
                if(_lightHelper.CurrentStateMode == StateMode.Out)
                {
                    MqttApplicationMessage message;
                    switch (type)
                    {
                        case SensorType.Door:
                            message = new MqttApplicationMessageBuilder()
                                .WithTopic("Home/Sensor/Door")
                                .WithPayload("1")
                                .WithAtLeastOnceQoS()
                                .Build();
                            await _mqttHelper.Publish(message);
                            //_logger.LogWarning("入侵报警,发送短信通知");
                            _notify.Send("门道");
                            break;
                        case SensorType.Aisle:
                            message = new MqttApplicationMessageBuilder()
                                .WithTopic("Home/Sensor/Aisle")
                                .WithPayload("1")
                                .WithAtLeastOnceQoS()
                                .Build();
                            await _mqttHelper.Publish(message);
                            _notify.Send("走道");
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        public async Task CloseDoor()
        {
            await _lightHelper.CloseDoor();
        }


        public enum SensorType
        {
            Door,
            Aisle,
            Smoke
        }
    }
}
