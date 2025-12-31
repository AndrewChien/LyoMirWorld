using MirCommon;
using MirCommon.Network;
using MirCommon.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// 类型别名
using Player = GameServer.HumanPlayer;

namespace GameServer
{
    public partial class GameClient
    {

        private async Task HandleWalk(byte[] data)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 走路1");
            try
            {
                // 解析移动数据
                var reader = new PacketReader(data);
                int x = reader.ReadInt32();
                int y = reader.ReadInt32();
                byte dir = reader.ReadByte();

                _player.X = (ushort)x;
                _player.Y = (ushort)y;

                // 通知周围玩家
                var map = _world.GetMap(_player.MapId);
                if (map != null)
                {
                    var nearbyPlayers = map.GetPlayersInRange(_player.X, _player.Y, 15);
                    // 发送移动消息给周围玩家
                    // 构建移动消息包并发送给视野范围内的玩家
                    foreach (var nearbyPlayer in nearbyPlayers)
                    {
                        if (nearbyPlayer != _player && nearbyPlayer != null)
                        {
                            // 这里需要发送SM_WALK消息给其他玩家
                            // 让他们看到当前玩家的移动
                        }
                    }
                }
            }
            catch { }

            await Task.CompletedTask;
        }

        private async Task HandleRun(byte[] data)
        {
            // 类似Walk处理
            await HandleWalk(data);
        }

        private async Task HandleSay(byte[] data)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 说话");
            try
            {
                string message = System.Text.Encoding.GetEncoding("GBK").GetString(data).TrimEnd('\0');
                LogManager.Default.Info($"[{_player.Name}]: {message}");

                // 广播聊天消息给周围玩家
                var map = _world.GetMap(_player.MapId);
                if (map != null)
                {
                    var nearbyPlayers = map.GetPlayersInRange(_player.X, _player.Y, 15);
                    foreach (var nearbyPlayer in nearbyPlayers)
                    {
                        if (nearbyPlayer != null)
                        {
                            // 发送SM_CHAT消息给附近玩家
                            // 让他们看到当前玩家的聊天内容
                        }
                    }
                }
            }
            catch { }

            await Task.CompletedTask;
        }

        // 新的消息处理方法
        private async Task HandleWalkMessage(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 走路2");
            // 从msg.dwFlag中提取坐标：低16位是x，高16位是y
            int x = (int)(msg.dwFlag & 0xFFFF);
            int y = (int)((msg.dwFlag >> 16) & 0xFFFF);
            byte dir = (byte)(msg.wParam[1] & 0xFF);

            bool success = _player.WalkXY(x, y);
            if (!success)
            {
                // 如果WalkXY失败，尝试Walk
                success = _player.Walk(dir);
            }

            // 发送移动确认
            SendActionResult(_player.X, _player.Y, success);

            await Task.CompletedTask;
        }

        private async Task HandleRunMessage(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 跑步");
            // 从msg.dwFlag中提取坐标：低16位是x，高16位是y
            int x = (int)(msg.dwFlag & 0xFFFF);
            int y = (int)((msg.dwFlag >> 16) & 0xFFFF);
            byte dir = (byte)(msg.wParam[1] & 0xFF);

            bool success = _player.RunXY(x, y);
            if (!success)
            {
                // 如果RunXY失败，尝试Run
                success = _player.Run(dir);
            }

            // 发送移动确认
            SendActionResult(_player.X, _player.Y, success);

            await Task.CompletedTask;
        }

        private async Task HandleSayMessage(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            try
            {
                string message = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
                LogManager.Default.Info($"处理客户端[{_player.Name}] 说话: {message}");

                // 广播聊天消息给周围玩家
                var map = _world.GetMap(_player.MapId);
                if (map != null)
                {
                    var nearbyPlayers = map.GetPlayersInRange(_player.X, _player.Y, 15);
                    foreach (var nearbyPlayer in nearbyPlayers)
                    {
                        if (nearbyPlayer != null)
                        {
                            // 发送SM_CHAT消息给附近玩家
                            SendChatMessage(nearbyPlayer, _player.Name, message);
                        }
                    }
                }
            }
            catch { }

            await Task.CompletedTask;
        }

        private async Task HandleTurnMessage(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 转向");
            byte dir = (byte)(msg.wParam[1] & 0xFF);

            bool success = _player.Turn(dir);

            // 发送转向确认
            SendActionResult(_player.X, _player.Y, success);

            await Task.CompletedTask;
        }

        private async Task HandleAttackMessage(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 攻击");
            byte dir = (byte)(msg.wParam[1] & 0xFF);

            bool success = _player.Attack(dir);

            // 发送攻击确认
            SendActionResult(_player.X, _player.Y, success);

            await Task.CompletedTask;
        }

        private async Task HandleGetMealMessage(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 挖肉");
            byte dir = (byte)(msg.wParam[1] & 0xFF);

            bool success = _player.GetMeal(dir);

            // 发送挖肉确认
            SendActionResult(_player.X, _player.Y, success);

            await Task.CompletedTask;
        }

        private async Task HandleStopMessage(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 停止移动");

            // 发送停止确认
            SendStopMessage();

            await Task.CompletedTask;
        }

        private async Task HandleConfirmFirstDialog(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 确认第一个对话框");

            _state = ClientState.GSUM_VERIFIED;
            LogManager.Default.Info($"已设置状态为GSUM_VERIFIED");

            // 将玩家添加到地图
            if (!GameWorld.Instance.AddMapObject(_player))
            {
                //Disconnect();
                return;
            }

            // 查询数据库信息
            using var dbClient = new MirCommon.Database.DBServerClient(_dbServerAddress, _dbServerPort);
            if (!await dbClient.ConnectAsync())
            {
                SendMsg(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "数据库错误！");
                //Disconnect(1000);
                return;
            }

            //lyo：以下切回去
            ////使用长连接
            //var dbClient = _server.GetDbServerClient();

            // 服务器ID，默认为1
            uint serverId = 1;

            // 查询技能数据
            LogManager.Default.Info($"向DBServer发送 DM_QUERYMAGIC 查询技能数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
            await dbClient.SendQueryMagic(serverId, _clientKey, _player.GetDBId());

            // 查询装备数据
            LogManager.Default.Info($"向DBServer发送 DM_QUERYITEMS 查询装备数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
            await dbClient.SendQueryItem(serverId, _clientKey, _player.GetDBId(), (byte)ItemDataFlag.IDF_EQUIPMENT, 20);

            // 查询升级物品数据
            LogManager.Default.Info($"向DBServer发送 DM_QUERYUPGRADEITEM 查询升级物品数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
            await dbClient.SendQueryUpgradeItem(serverId, _clientKey, _player.GetDBId());

            // 查询背包数据
            // 注意：Inventory类可能没有GetCountLimit方法，使用MaxSlots代替
            int bagLimit = 40; // 默认背包大小
            if (_player.GetBag() != null)
            {
                // 尝试获取背包限制，如果Inventory类有MaxSlots属性则使用它
                // 否则使用默认值
                bagLimit = 40; // 默认值
            }
            LogManager.Default.Info($"向DBServer发送 DM_QUERYITEMS 查询背包数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
            await dbClient.SendQueryItem(serverId, _clientKey, _player.GetDBId(), (byte)ItemDataFlag.IDF_BAG, bagLimit);

            // 查询仓库数据
            LogManager.Default.Info($"向DBServer发送 DM_QUERYITEMS 查询仓库数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
            await dbClient.SendQueryItem(serverId, _clientKey, _player.GetDBId(), (byte)ItemDataFlag.IDF_BANK, 100);

            // 查询宠物仓库数据
            LogManager.Default.Info($"向DBServer发送 DM_QUERYITEMS 查询宠物仓库数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
            await dbClient.SendQueryItem(serverId, _clientKey, _player.GetDBId(), (byte)ItemDataFlag.IDF_PETBANK, 10);

            // 查询任务信息
            LogManager.Default.Info($"向DBServer发送 DM_QUERYTASKINFO 查询任务数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
            await dbClient.QueryTaskInfo(serverId, _clientKey, _player.GetDBId());

            // 如果是第一次登录，添加首次登录处理
            if (_player.IsFirstLogin)
            {
                LogManager.Default.Info($"向客户端发送 EP_FIRSTLOGINPROCESS");
                _player.AddProcess(EP_FIRSTLOGINPROCESS, 0, 0, 0, 0, 20, 1, null);
            }

            LogManager.Default.Info($"已处理确认第一个对话框，等待数据库响应: {_player.Name}");

            await Task.CompletedTask;
        }

        private async Task HandleSelectLink(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            string link = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 选择链接: {link}");

            // 处理NPC链接选择
            NPCMessageHandler.HandleSelectLink(_player, (uint)msg.dwFlag, link);

            await Task.CompletedTask;
        }

        private async Task HandleTakeOnItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            int pos = msg.wParam[0];
            uint itemId = msg.dwFlag;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 穿戴物品 位置:{pos} 物品ID:{itemId}");

            // 发送穿戴结果
            SendEquipItemResult(true, pos, itemId);

            await Task.CompletedTask;
        }

        private async Task HandleTakeOffItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            int pos = msg.wParam[0];
            uint itemId = msg.dwFlag;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 脱下物品 位置:{pos} 物品ID:{itemId}");

            // 发送脱下结果
            SendUnEquipItemResult(true, pos, itemId);

            await Task.CompletedTask;
        }

        private async Task HandleDropItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            uint itemId = msg.dwFlag;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 丢弃物品 物品ID:{itemId}");

            // 发送丢弃结果
            SendDropItemResult(true, itemId);

            await Task.CompletedTask;
        }

        private async Task HandlePickupItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 拾取物品");

            // 发送拾取结果
            SendPickupItemResult(true);

            await Task.CompletedTask;
        }

        // 新增消息处理方法
        private async Task HandleSpellSkill(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 技能施放");
            // 解析技能数据
            int x = (int)(msg.dwFlag & 0xFFFF);
            int y = (int)((msg.dwFlag >> 16) & 0xFFFF);
            uint magicId = (uint)msg.wParam[0];
            ushort targetId = (ushort)msg.wParam[1];

            // 调用HumanPlayer的SpellCast方法
            bool success = _player.SpellCast(x, y, magicId, targetId);

            // 发送技能施放确认
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleQueryTrade(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 请求交易");
            await Task.CompletedTask;
        }

        private async Task HandlePutTradeItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 放入交易物品");
            await Task.CompletedTask;
        }

        private async Task HandlePutTradeGold(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 放入交易金币");
            await Task.CompletedTask;
        }

        private async Task HandleQueryTradeEnd(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 确认交易结束");
            await Task.CompletedTask;
        }

        private async Task HandleCancelTrade(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 取消交易");
            await Task.CompletedTask;
        }

        private async Task HandleChangeGroupMode(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 更改组队模式");
            await Task.CompletedTask;
        }

        private async Task HandleQueryAddGroupMember(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 请求添加组队成员");
            await Task.CompletedTask;
        }

        private async Task HandleDeleteGroupMember(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 删除组队成员");
            await Task.CompletedTask;
        }

        private async Task HandleQueryStartPrivateShop(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 请求开启个人商店");
            await Task.CompletedTask;
        }

        private async Task HandleZuoyi(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 作揖/切换聊天频道");
            await Task.CompletedTask;
        }

        private async Task HandlePing(MirMsgOrign msg, byte[] payload)
        {
            // 发送ping响应
            LogManager.Default.Info($"处理客户端Ping响应：{msg.dwFlag}");
            GameMessageHandler.SendSimpleMessage2(_stream, msg.dwFlag,
                GameMessageHandler.ServerCommands.SM_PINGRESPONSE, 0, 0, 0);
            await Task.CompletedTask;
        }

        private async Task HandleQueryTime(MirMsgOrign msg, byte[] payload)
        {
            // 发送时间响应
            uint currentTime = (uint)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
            LogManager.Default.Info($"处理客户端时间响应：{currentTime}");
            GameMessageHandler.SendSimpleMessage2(_stream, currentTime,
                GameMessageHandler.ServerCommands.SM_TIMERESPONSE, 0, 0, 0);
            await Task.CompletedTask;
        }

        private async Task HandleRideHorse(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 骑马");
            // 发送骑马响应
            GameMessageHandler.SendSimpleMessage2(_stream, 1,
                GameMessageHandler.ServerCommands.SM_RIDEHORSERESPONSE, 0, 0, 0);
            await Task.CompletedTask;
        }

        private async Task HandleUseItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 使用物品");
            uint makeIndex = msg.dwFlag;

            // 调用HumanPlayer的UseItem方法
            bool success = _player.UseItem(makeIndex);

            // 发送使用物品结果
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleDropGold(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 丢弃金币");
            uint amount = msg.dwFlag;

            // 调用HumanPlayer的DropGold方法
            bool success = _player.DropGold(amount);

            // 发送丢弃金币结果
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleNPCTalk(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] NPC对话");
            await Task.CompletedTask;
        }

        private async Task HandleBuyItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 购买");
            uint npcInstanceId = msg.dwFlag;
            int itemIndex = msg.wParam[0];

            // 调用HumanPlayer的BuyItem方法
            bool success = _player.BuyItem(npcInstanceId, itemIndex);

            // 发送购买结果
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleSellItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 出售");
            uint npcInstanceId = msg.dwFlag;
            int bagSlot = msg.wParam[0];

            // 调用HumanPlayer的SellItem方法
            bool success = _player.SellItem(npcInstanceId, bagSlot);

            // 发送出售结果
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleRepairItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 修理");
            uint npcInstanceId = msg.dwFlag;
            int bagSlot = msg.wParam[0];

            // 调用HumanPlayer的RepairItem方法
            bool success = _player.RepairItem(npcInstanceId, bagSlot);

            // 发送修理结果
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleQueryRepairPrice(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 查询修理价格");
            uint npcInstanceId = msg.dwFlag;
            int bagSlot = msg.wParam[0];

            // 调用HumanPlayer的QueryRepairPrice方法
            bool success = _player.QueryRepairPrice(npcInstanceId, bagSlot);

            // 发送查询结果
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleQueryMinimap(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 查询小地图");
            // 发送小地图信息
            GameMessageHandler.SendSimpleMessage2(_stream, _player.ObjectId,
                GameMessageHandler.ServerCommands.SM_MINIMAP, 0, 0, 0);
            await Task.CompletedTask;
        }

        private async Task HandleViewEquipment(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 查看装备");
            uint targetPlayerId = msg.dwFlag;

            // 调用HumanPlayer的ViewEquipment方法
            bool success = _player.ViewEquipment(targetPlayerId);

            // 发送查看装备结果
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleMine(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 挖矿");
            byte dir = (byte)(msg.wParam[1] & 0xFF);

            // 调用HumanPlayer的DoMine方法
            bool success = _player.DoMine(dir);

            // 发送挖矿确认
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleTrainHorse(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 训练马匹");
            byte dir = (byte)(msg.wParam[1] & 0xFF);

            // 调用HumanPlayer的DoTrainHorse方法
            bool success = _player.DoTrainHorse(dir);

            // 发送训练马匹确认
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleSpecialHit(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 特殊攻击");
            byte dir = (byte)(msg.wParam[1] & 0xFF);
            int skillType = msg.wCmd switch
            {
                GameMessageHandler.ClientCommands.CM_SPECIALHIT_KILL => 7,      // 攻杀
                GameMessageHandler.ClientCommands.CM_SPECIALHIT_ASSASSINATE => 12, // 刺杀
                GameMessageHandler.ClientCommands.CM_SPECIALHIT_HALFMOON => 25,   // 半月
                GameMessageHandler.ClientCommands.CM_SPECIALHIT_FIRE => 26,       // 烈火
                GameMessageHandler.ClientCommands.CM_SPECIALHIT_POJISHIELD => 0,  // 破击/破盾
                _ => 0
            };

            // 调用HumanPlayer的SpecialHit方法
            bool success = _player.SpecialHit(dir, skillType);

            // 发送特殊攻击确认
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleLeaveServer(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 离开服务器");

            // 断开客户端连接
            Disconnect();

            await Task.CompletedTask;
        }

        private async Task HandleUnknown45(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 处理未知命令 0x45");

            // 发送确认响应
            GameMessageHandler.SendSimpleMessage2(_stream, 0,
                GameMessageHandler.ServerCommands.SM_PINGRESPONSE, 0, 0, 0);

            await Task.CompletedTask;
        }

        private async Task HandlePutItemToPetBag(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 放入宠物仓库 物品ID:{msg.dwFlag}");
            
            _player.PutItemToPetBag(msg.dwFlag);
            
            await Task.CompletedTask;
        }

        private async Task HandleGetItemFromPetBag(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 从宠物仓库取出 物品ID:{msg.dwFlag}");
            
            _player.GetItemFromPetBag(msg.dwFlag);
            
            await Task.CompletedTask;
        }

        private async Task HandleDeleteTask(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 删除任务 任务ID:{msg.dwFlag}");
            
            _player.DeleteTask(msg.dwFlag);
            
            await Task.CompletedTask;
        }

        private async Task HandleGMTestCommand(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] GM命令测试");
            
            LogManager.Default.Info($"GM测试命令: Flag={msg.dwFlag}, wParam0={msg.wParam[0]}, wParam1={msg.wParam[1]}");
            
            await Task.CompletedTask;
        }

        private async Task HandleCompletelyQuit(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 完全退出");
            
            _competlyQuit = true;
            
            // 断开连接
            Disconnect();
            
            await Task.CompletedTask;
        }

        private async Task HandleCutBody(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 切割尸体 Flag:{msg.dwFlag}, wParam0:{msg.wParam[0]}, wParam1:{msg.wParam[1]}, wParam2:{msg.wParam[2]}");
            
            bool success = _player.CutBody(msg.dwFlag, msg.wParam[0], msg.wParam[1], msg.wParam[2]);
            
            if (success)
            {
                LogManager.Default.Info($"切割尸体成功，发送周围消息");
            }
            
            await Task.CompletedTask;
        }

        private async Task HandlePutItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 放入物品 Flag:{msg.dwFlag}, wParam0:{msg.wParam[0]}");
            
            uint param = (uint)((msg.wParam[1] << 16) | msg.wParam[0]);
            _player.OnPutItem(msg.dwFlag, param);
            
            await Task.CompletedTask;
        }

        private async Task HandleShowPetInfo(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 显示宠物信息");
            
            _player.ShowPetInfo();
            
            await Task.CompletedTask;
        }

        private async Task HandleMarketMessage(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 市场消息 wParam0:{msg.wParam[0]}, wParam1:{msg.wParam[1]}, wParam2:{msg.wParam[2]}");
            
            LogManager.Default.Info($"市场消息处理: 参数={msg.wParam[0]},{msg.wParam[1]},{msg.wParam[2]}, 数据长度={payload.Length}");
            
            await Task.CompletedTask;
        }

        private async Task HandleDeleteFriend(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            string friendName = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 删除好友: {friendName}");
            
            _player.DeleteFriend(friendName);
            
            await Task.CompletedTask;
        }

        private async Task HandleReplyAddFriendRequest(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            string replyData = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 回复添加好友请求 Flag:{msg.dwFlag}, 数据:{replyData}");
            
            _player.ReplyAddFriendRequest(msg.dwFlag, replyData);
            
            await Task.CompletedTask;
        }

        private async Task HandleAddFriend(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            string friendName = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 添加好友: {friendName}");
            
            var targetPlayer = HumanPlayerMgr.Instance.FindByName(friendName);
            if (targetPlayer != null)
            {
                // 发送添加好友请求
                targetPlayer.PostAddFriendRequest(_player);
                LogManager.Default.Info($"已向 {friendName} 发送添加好友请求");
            }
            else
            {
                // 玩家不在线
                _player.SendFriendSystemError(1, friendName); // FE_ADD_OFFONLINE = 1
                LogManager.Default.Info($"玩家 {friendName} 不在线");
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleCreateGuildOrInputConfirm(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            string inputData = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 创建行会/输入确认: {inputData}");
            
            LogManager.Default.Info($"输入确认处理: {inputData}");
            
            await Task.CompletedTask;
        }

        private async Task HandleReplyAddToGuildRequest(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            bool accept = msg.wParam[0] != 0;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 回复加入行会请求: {(accept ? "接受" : "拒绝")}");
            
            _player.ReplyAddToGuildRequest(accept);
            
            await Task.CompletedTask;
        }

        private async Task HandleInviteToGuild(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            string memberName = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 邀请加入行会: {memberName}");
            
            var guild = _player.Guild;
            if (guild != null && guild.IsMaster(_player))
            {
                var targetPlayer = HumanPlayerMgr.Instance.FindByName(memberName);
                if (targetPlayer != null)
                {
                    if (targetPlayer.Guild != null)
                    {
                        _player.SaySystem("对方已经是其他行会成员");
                    }
                    else
                    {
                        // 发送加入行会请求
                        targetPlayer.PostAddToGuildRequest(_player);
                        LogManager.Default.Info($"已向 {memberName} 发送加入行会请求");
                    }
                }
                else
                {
                    _player.SaySystem("玩家不存在");
                }
            }
            else
            {
                _player.SaySystem("只有行会会长才能邀请成员");
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleTakeBankItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            uint npcId = msg.dwFlag;
            uint itemId = (uint)((msg.wParam[1] << 16) | msg.wParam[0]);
            LogManager.Default.Info($"处理客户端[{_player.Name}] 从仓库取出 NPC:{npcId}, 物品ID:{itemId}");
            
            // 这里需要检查NPC是否存在并调用取出方法
            bool success = _player.TakeBankItem(itemId);
            
            if (success)
            {
                SendMsg(itemId, GameMessageHandler.ServerCommands.SM_BANKTAKEOK, 0, 0, 0); // 取出成功
            }
            else
            {
                SendMsg(itemId, GameMessageHandler.ServerCommands.SM_BANKTAKEFAIL, 0, 0, 0); // 取出失败
            }
            
            await Task.CompletedTask;
        }

        private async Task HandlePutBankItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            uint npcId = msg.dwFlag;
            uint itemId = (uint)((msg.wParam[1] << 16) | msg.wParam[0]);
            LogManager.Default.Info($"处理客户端[{_player.Name}] 放入仓库 NPC:{npcId}, 物品ID:{itemId}");
            
            // 这里需要检查NPC是否存在并调用放入方法
            bool success = _player.PutBankItem(itemId);
            
            if (success)
            {
                SendMsg(itemId, GameMessageHandler.ServerCommands.SM_BANKPUTOK, 0, 0, 0); // 放入成功
            }
            else
            {
                SendMsg(itemId, GameMessageHandler.ServerCommands.SM_BANKPUTFAIL, 0, 0, 0); // 放入失败
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleQueryCommunity(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 查询社区信息");

            // 这里需要调用数据库查询社区信息
            //using var dbClient = new MirCommon.Database.DBServerClient(_dbServerAddress, _dbServerPort);


            //使用长连接
            var dbClient = _server.GetDbServerClient();
            if (dbClient != null)
            {
                LogManager.Default.Info($"查询社区信息: 玩家ID={_player.GetDBId()}");
            }
            else
            {
                SendMsg(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "数据库错误！");
                //Disconnect(1000);
            }

            await Task.CompletedTask;
        }

        private async Task HandleDeleteGuildMember(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            string memberName = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 删除行会成员: {memberName}");
            
            var guild = _player.Guild;
            if (guild != null && guild.IsMaster(_player))
            {
                guild.RemoveMember(memberName);
                LogManager.Default.Info($"已删除行会成员: {memberName}");
            }
            else
            {
                _player.SaySystem("只有行会会长才能删除成员");
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleEditGuildNotice(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            string notice = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 编辑行会公告: {notice}");
            
            var guild = _player.Guild;
            if (guild != null && guild.IsMaster(_player))
            {
                notice += "\r";
                guild.SetNotice(notice);
                
                // 发送行会首页
                string frontPage = guild.GetFrontPage();
                SendMsg(0, GameMessageHandler.ServerCommands.SM_GUILDFRONTPAGE, 0, 0, 0, frontPage);
                LogManager.Default.Info($"已更新行会公告");
            }
            else
            {
                SendMsg(0, GameMessageHandler.ServerCommands.SM_GUILDFRONTPAGEFAIL, 0, 0, 0); // 尚未加入门派
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleEditGuildTitle(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            string memberList = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 编辑行会封号: {memberList}");
            
            var guild = _player.Guild;
            if (guild != null && guild.IsMaster(_player))
            {
                bool success = guild.ParseMemberList(_player, memberList);
                if (!success)
                {
                    _player.SaySystem(guild.GetErrorMsg());
                }
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleQueryGuildExp(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 查询行会经验");
            
            var guild = _player.Guild;
            if (guild != null)
            {
                guild.SendExp(_player);
            }
            else
            {
                SendMsg(0, GameMessageHandler.ServerCommands.SM_GUILDFRONTPAGEFAIL, 0, 0, 0); // 尚未加入门派
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleQueryGuildInfo(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 请求行会信息");
            
            var guild = _player.Guild;
            if (guild != null)
            {
                guild.SendFirstPage(_player);
            }
            else
            {
                SendMsg(0, GameMessageHandler.ServerCommands.SM_GUILDFRONTPAGEFAIL, 0, 0, 0); // 尚未加入门派
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleQueryGuildMemberList(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 请求行会成员列表");
            
            var guild = _player.Guild;
            if (guild != null)
            {
                guild.SendMemberList(_player);
            }
            else
            {
                SendMsg(0, GameMessageHandler.ServerCommands.SM_GUILDFRONTPAGEFAIL, 0, 0, 0); // 尚未加入门派
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleQueryHistoryAddress(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 查询历史地址");
            
            // 这里需要调用数据库查询历史地址
            //using var dbClient = new MirCommon.Database.DBServerClient(_dbServerAddress, _dbServerPort);

            //使用长连接
            var dbClient = _server.GetDbServerClient();
            if (dbClient != null)
            {
                LogManager.Default.Info($"查询历史地址: 玩家ID={_player.GetDBId()}");
            }
            else
            {
                SendMsg(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "数据库错误！");
                //Disconnect(1000);
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleSetMagicKey(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 设置技能快捷键 Flag:{msg.dwFlag}, wParam0:{msg.wParam[0]}, wParam1:{msg.wParam[1]}");
            
            _player.SetMagicKey(msg.dwFlag, msg.wParam[0], msg.wParam[1]);
            
            await Task.CompletedTask;
        }

        private async Task HandleSetBagItemPos(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 设置背包物品位置");
            
            // 这里需要解析payload中的BAGITEMPOS数组
            LogManager.Default.Info($"设置背包物品位置: 数据长度={payload.Length}");
            
            await Task.CompletedTask;
        }

        private async Task HandleNPCTalkOrViewPrivateShop(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] NPC对话/查看个人商店 Flag:{msg.dwFlag}");
            
            if (msg.dwFlag == 0)
            {
                // NPC对话
                LogManager.Default.Info($"NPC对话处理");
            }
            else
            {
                // 查看个人商店
                LogManager.Default.Info($"查看个人商店: 商店ID={msg.dwFlag}");
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleRestartGame(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 重启游戏");
            
            // 这里需要保存玩家数据并重新加载
            _player.UpdateToDB(); // 保存数据到数据库
                        
            await Task.CompletedTask;
        }

        private async Task HandlePingResponse(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] Ping响应: {msg.dwFlag}");
            
            GameMessageHandler.SendSimpleMessage2(_stream, msg.dwFlag,
                GameMessageHandler.ServerCommands.SM_PINGRESPONSE, 0, 0, 0);
            
            await Task.CompletedTask;
        }
    }
}
