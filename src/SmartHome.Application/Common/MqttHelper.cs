using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace SmartHome.Application
{
    /// <summary>
    /// mqtt 消息总线
    /// </summary>
    public class MqttHelper : BackgroundService
    {
        
        private readonly ILogger<MqttHelper> _logger;
        private readonly IConfiguration _config;
        private readonly CurtainHelper _curtainHelper;
        private readonly HvacHelper _hvacHelper;
        private readonly LightHelper _lightHelper;

        private bool Started = false;
        private IMqttClientOptions options;
        private MqttClient _mqttClient;

        public MqttHelper(CurtainHelper curtainHelper, HvacHelper hvacHelper, IConfiguration configuration,LightHelper lightHelper,ILogger<MqttHelper> logger)
        {
            _config = configuration;
            _logger = logger;
            _curtainHelper = curtainHelper;
            _hvacHelper = hvacHelper;
            _lightHelper = lightHelper;
            _hvacHelper.SetMqttListener(this);
            _curtainHelper.SetMqttListener(this);
            _lightHelper.SetMqttListener(this);
            //_lightHelper.SetCurtainHelper(_curtainHelper);

            var mqtthost = _config.GetValue<string>("mqttBroken:Hostip");
            var port = _config.GetValue<int>("mqttBroken:port");

            _logger.LogInformation ("Connect to MQTT Broken：{0}:{1}", mqtthost, port);

            options = new MqttClientOptionsBuilder()
           .WithClientId(Guid.NewGuid().ToString())
           .WithTcpServer(mqtthost, port)
           .Build();

            MqttFactory factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient() as MqttClient;

            _mqttClient.UseApplicationMessageReceivedHandler(e =>
            {

                var sVal = string.Empty;
                sVal = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                _logger.LogInformation("### 数据接收 ###");
                _logger.LogInformation($"+ Topic = {e.ApplicationMessage.Topic}");
                _logger.LogInformation($"+ Payload = {sVal}");
                _logger.LogInformation($"+ QoS = {e.ApplicationMessage.QualityOfServiceLevel}");
                _logger.LogInformation($"+ Retain = {e.ApplicationMessage.Retain}");
                _logger.LogInformation("");

                /*   窗帘  */
                #region 窗帘
                if (e.ApplicationMessage.Topic == "Home/Curtain/Set")
                {
                    var obj = JsonConvert.DeserializeObject<CurtainHelper.CurtainStateObject>(sVal);
                    Task.Run(async () => { await _curtainHelper.SetCurtain(obj.Id, obj.Status); });
                }
                else if(e.ApplicationMessage.Topic == "Home/Curtain/SetPosition2")
                {
                    int i = (int)float.Parse(sVal);
                    Task.Run(async () => { await _curtainHelper.SetCurtain(2, i); });
                }
                else if (e.ApplicationMessage.Topic == "Home/Curtain/SetPosition3")
                {
                    int i = (int)float.Parse(sVal);
                    _logger.LogInformation("value :{0}",i);
                    Task.Run(async () => { await _curtainHelper.SetCurtain(3, i); });
                }
                else if(e.ApplicationMessage.Topic == "Home/Curtain/Stop2")
                {
                    var task1 = Task.Run(async () =>
                    {
                        await _curtainHelper.Stop(2);
                        await Task.Delay(100);
                        await _curtainHelper.GetCurtainStatus(2);
                    });
                }
                else if (e.ApplicationMessage.Topic == "Home/Curtain/Stop3")
                {
                    var task1 = Task.Run(async () =>
                    {
                        await _curtainHelper.Stop(3);
                        await Task.Delay(100);
                        await _curtainHelper.GetCurtainStatus(3);
                    });
                }
                else if (e.ApplicationMessage.Topic == "Home/Curtain/Command")
                {
                    var obj = JsonConvert.DeserializeObject<CurtainHelper.CurtainStateObject>(sVal);
                    if (obj.Command == "open")
                    {
                        Task.Run(async () => { await _curtainHelper.Open(obj.Id); });
                    }
                    else if (obj.Command == "close")
                    {
                        Task.Run(async () => { await _curtainHelper.Close(obj.Id); });
                    }
                    else if (obj.Command == "stop")
                    {

                        var task1 = Task.Run(async () =>
                        {
                            await _curtainHelper.Stop(obj.Id);
                            await Task.Delay(100);
                            await _curtainHelper.GetCurtainStatus(obj.Id);
                        });
                    }
                }
                #endregion
                /*   空调  */
                #region 空调
                else if (e.ApplicationMessage.Topic == "Home/Mitsubishi/Command")
                {
                    var obj = JsonConvert.DeserializeObject<HvacStateObject>(sVal);
                    Task.Run(async () => { await _hvacHelper.UpdateStateObject(obj); });

                }
                //客房空调
                else if (e.ApplicationMessage.Topic == "Home/Sanling/00/SetState")
                {
                    if(sVal == "Off")
                    {
                        Task.Run(async () => { await _hvacHelper.TurnOffAC(0); });

                    }else if(sVal == "Heat")
                    {
                        Task.Run(async () => { 
                            await _hvacHelper.TurnOnAC(0);
                            await _hvacHelper.SetMode(0, WorkMode.Heat);
                        });
                    }
                    else // vVal == "Cool"
                    {
                        Task.Run(async () => {
                            await _hvacHelper.TurnOnAC(0);
                            await _hvacHelper.SetMode(0, WorkMode.Cool);
                        });
                    }
                }
                else if (e.ApplicationMessage.Topic == "Home/Sanling/00/SetTarget")
                {
                    var temp = float.Parse(sVal);
                    Task.Run(async () => { await _hvacHelper.SetTemperature(0, temp); });
                }
                //主卧空调
                else if (e.ApplicationMessage.Topic == "Home/Sanling/01/SetState")
                {
                    if (sVal == "Off")
                    {
                        Task.Run(async () => { await _hvacHelper.TurnOffAC(1); });

                    }
                    else if (sVal == "Heat")
                    {
                        Task.Run(async () => {
                            await _hvacHelper.TurnOnAC(1);
                            await _hvacHelper.SetMode(1, WorkMode.Heat);
                        });
                    }
                    else // vVal == "Cool"
                    {
                        Task.Run(async () => {
                            await _hvacHelper.TurnOnAC(1);
                            await _hvacHelper.SetMode(1, WorkMode.Cool);
                        });
                    }
                }
                else if (e.ApplicationMessage.Topic == "Home/Sanling/01/SetTarget")
                {
                    var temp = float.Parse(sVal);
                    Task.Run(async () => { await _hvacHelper.SetTemperature(1, temp); });
                }
                //书房空调
                else if (e.ApplicationMessage.Topic == "Home/Sanling/02/SetState")
                {
                    if (sVal.ToLower() == "off")
                    {
                        Task.Run(async () => { await _hvacHelper.TurnOffAC(2); });

                    }
                    else if (sVal.ToLower() == "heat")
                    {
                        Task.Run(async () => {
                            await _hvacHelper.TurnOnAC(2);
                            await _hvacHelper.SetMode(2, WorkMode.Heat);
                        });
                    }
                    else if(sVal.ToLower() == "cool")
                    {
                        Task.Run(async () => {
                            await _hvacHelper.TurnOnAC(2);
                            await _hvacHelper.SetMode(2, WorkMode.Cool);
                        });
                    }
                }
                else if (e.ApplicationMessage.Topic == "Home/Sanling/02/SetTarget")
                {
                    var temp = float.Parse(sVal);
                    Task.Run(async () => { await _hvacHelper.SetTemperature(2, temp); });
                }
                //客厅空调
                else if (e.ApplicationMessage.Topic == "Home/Sanling/03/SetState")
                {
                    if (sVal == "Off")
                    {
                        Task.Run(async () => { await _hvacHelper.TurnOffAC(3); });

                    }
                    else if (sVal == "Heat")
                    {
                        Task.Run(async () => {
                            await _hvacHelper.TurnOnAC(3);
                            await _hvacHelper.SetMode(3, WorkMode.Heat);
                        });
                    }
                    else // vVal == "Cool"
                    {
                        Task.Run(async () => {
                            await _hvacHelper.TurnOnAC(3);
                            await _hvacHelper.SetMode(3, WorkMode.Cool);
                        });
                    }
                }
                else if (e.ApplicationMessage.Topic == "Home/Sanling/03/SetTarget")
                {
                    var temp = float.Parse(sVal);
                    Task.Run(async () => { await _hvacHelper.SetTemperature(3, temp); });
                }
                #endregion
                /*   灯光  */
                #region 灯光
                else if (e.ApplicationMessage.Topic == "Home/LightScene/Livingroom")
                {
                    int i = (int)float.Parse(sVal);
                    Task.Run(async () => { await _lightHelper.SceneLivingRoomSet((SceneState)i); });
                }
                else if (e.ApplicationMessage.Topic == "Home/LightScene/Bedroom")
                {
                    int i = (int)float.Parse(sVal);
                    Task.Run(async () => { await _lightHelper.SceneBedRoomSet((SceneState)i); });
                }
                else if (e.ApplicationMessage.Topic == "Home/LightScene/Guestroom")
                {
                    int i = (int)float.Parse(sVal);
                    Task.Run(async () => { await _lightHelper.SceneGuestRoomSet((SceneState)i); });
                }
                else if (e.ApplicationMessage.Topic == "Home/LightScene/Workroom")
                {
                    int i = (int)float.Parse(sVal);
                    Task.Run(async () => { await _lightHelper.SceneWorkRoomSet((SceneState)i); });
                }
                else if (e.ApplicationMessage.Topic == "Home/LightScene/Dinner")
                {
                    int i = (int)float.Parse(sVal);
                    Task.Run(async () => { await _lightHelper.SceneDinnerRoomSet((SceneState)i); });
                }
                #endregion
                else if (e.ApplicationMessage.Topic == "Home/Mode")
                {
                    int i = int.Parse(sVal);
                    _lightHelper.CurrentStateMode = (StateMode)i;
                    if (i == 0)
                    {
                        Task.Run(async () => { await _lightHelper.HomeMode(); });
                    } else if (i == 1)
                    {
                        Task.Run(async () => { await _lightHelper.OutMode(); });
                    } else if (i == 2)
                    {
                        Task.Run(async () => { await _lightHelper.ReadMode(); });
                    }
                }
                else if (e.ApplicationMessage.Topic == "Home/Sensor/Motion/1")
                {
                    if(sVal == "ON")
                    {
                        Task.Run(async () => { await _lightHelper.OpenWindowLight(1); });
                    }
                    else
                    {
                        Task.Run(async () => { await _lightHelper.CloseWindowLight(1); });
                    }
                    
                }
                else if (e.ApplicationMessage.Topic == "Home/Sensor/Motion/2")
                {
                    if (sVal == "ON")
                    {
                        Task.Run(async () => { await _lightHelper.OpenWindowLight(2); });
                    }
                    else
                    {
                        Task.Run(async () => { await _lightHelper.CloseWindowLight(2); });
                    }
                }
                
            });

            _mqttClient.UseDisconnectedHandler(async e => {
                await _mqttClient.ReconnectAsync();
                SetupSubscribe();
            });

            
        }


        public async void Subscribe(string topic)
        {
            await _mqttClient.SubscribeAsync(topic, MqttQualityOfServiceLevel.AtLeastOnce);
        }

        public async Task Publish(MqttApplicationMessage message)
        {
            try
            {
                if(!_mqttClient.IsConnected && Started == true)
                {
                    await _mqttClient.ReconnectAsync();
                    //SetupSubscribe();
                }
                await _mqttClient.PublishAsync(message);
                _logger.LogInformation(message.Topic);
            }
            catch (Exception ex)
            {

                _logger.LogError(ex.Message);
            }

        }

        private void SetupSubscribe()
        {
            Subscribe("Home/Curtain/Set"); //设置窗帘开合百分比
            Subscribe("Home/Curtain/SetPosition2");
            Subscribe("Home/Curtain/SetPosition3");
            Subscribe("Home/Curtain/Stop3");
            Subscribe("Home/Curtain/Stop2");
            Subscribe("Home/Curtain/GetStatus");
            Subscribe("Home/Curtain/Command"); //接收命令:open,close,stop
            Subscribe("Home/Hailin/GetState");
            Subscribe("Home/Hailin/Command");
            Subscribe("Home/Mitsubishi/Command");
            Subscribe("Home/Sanling/00/SetState");
            Subscribe("Home/Sanling/00/SetTarget");
            Subscribe("Home/Sanling/01/SetState");
            Subscribe("Home/Sanling/01/SetTarget");
            Subscribe("Home/Sanling/02/SetState");
            Subscribe("Home/Sanling/02/SetTarget");
            Subscribe("Home/Sanling/03/SetState");
            Subscribe("Home/Sanling/03/SetTarget");
            Subscribe("Home/LightScene/Livingroom");
            Subscribe("Home/LightScene/Workroom");
            Subscribe("Home/LightScene/Bedroom");
            Subscribe("Home/LightScene/Guestroom");
            Subscribe("Home/LightScene/Dinner");
            Subscribe("Home/Mode");
            Subscribe("Home/Sensor/Motion/1");
            Subscribe("Home/Sensor/Motion/2");
        }

        public async Task StartAsync()
        {
            try
            {
                
                var result = await _mqttClient.ConnectAsync(options);
                if (result.ResultCode == MQTTnet.Client.Connecting.MqttClientConnectResultCode.Success)
                {
                    SetupSubscribe();
                }
                Started = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
        }


        public override void Dispose()
        {
            if (_mqttClient != null)
            {
                if (_mqttClient.IsConnected)
                {
                    var task = new Task(() => { _mqttClient.DisconnectAsync(); });
                    task.RunSynchronously();
                }
                _mqttClient.Dispose();
            }
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return StartAsync();


        }
    }
}
