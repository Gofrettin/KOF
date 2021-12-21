﻿using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using KOF.Common;
using KOF.Models;
using KOF.UI;

namespace KOF.Core
{
    public class Client : Processor
    {
        private Dispatcher _DispatcherInterface { get; set; }
        private int _MiningEventTime { get; set; } = Environment.TickCount;
        private int _MonsterStoneEventTime { get; set; } = Environment.TickCount;
        private int _RepairEventTime { get; set; } = Environment.TickCount;
        private int _SupplyEventTime { get; set; } = Environment.TickCount;
        private int _CheatEventTime { get; set; } = Environment.TickCount;
        private int _ProtectionEventTime { get; set; } = Environment.TickCount;
        private int _AttackEventTime { get; set; } = Environment.TickCount;
        private int _TimedSkillEventTime { get; set; } = Environment.TickCount;
        private int _TargetEventTime { get; set; } = Environment.TickCount;
        private int _ActionEventTime { get; set; } = Environment.TickCount;
        private bool _Started { get; set; } = false;
        private int _BlackMarketerEventTime { get; set; } = Environment.TickCount;
        private List<Thread> _ThreadPool { get; set; } = new List<Thread>();

        public Client(App App, Dispatcher DispatcherInterface)
        {
            _App = App;

            _DispatcherInterface = DispatcherInterface;

            StartThread(ClientEvent);
            StartThread(RecvPacketEvent);
            StartThread(SendPacketEvent);
        }

        private void StartThread(Action p)
        {
            Thread t = new Thread(new ThreadStart(p));

            t.IsBackground = true;
            t.Start();

            _ThreadPool.Add(t);
        }

        public Dispatcher GetDispatcherInterface()
        {
            return _DispatcherInterface;
        }

        public void Start()
        {
            SetNameConst(GetName());

            Storage.ControlCollection.Add(GetNameConst(), Database().GetControlList(GetNameConst()));

            Storage.SkillCollection.Add(GetNameConst(), Database().GetSkillList(GetJob()));

            Storage.SkillBarCollection.Add(GetNameConst(), Database().GetSkillBarList(GetNameConst(), GetPlatform().ToString()));

            Storage.LootCollection.Add(GetNameConst(), Database().GetLootList(GetNameConst(), GetPlatform().ToString()));

            Storage.SellCollection.Add(GetNameConst(), Database().GetSellList(GetNameConst(), GetPlatform().ToString()));

            _DispatcherInterface.InitializeControl();

            StartThread(CharacterEvent);
            StartThread(ProtectionEvent);
            StartThread(AttackEvent);
            StartThread(TimedSkillEvent);
            StartThread(PartyEvent);
            StartThread(RepairEvent);
            StartThread(SupplyEvent);
            StartThread(TargetAndActionEvent);
            StartThread(MobClearEvent);
            StartThread(MiningEvent);
            StartThread(MonsterStoneEvent);
            StartThread(AutoLootEvent);

            _Started = true;
        }

        public void Save()
        {
            string User = GetNameConst();

            if (User != "")
            {
                Database().SaveControl(User);
                Database().SaveSkillBar(User);
                Database().SaveSell(User);
                Database().SaveLoot(User);
            }
        }

        public void Destroy()
        {
            Save();

            Storage.ClientCollection.Remove(GetProcessId());
            Storage.ControlCollection.Remove(GetNameConst());
            Storage.SkillCollection.Remove(GetNameConst());
            Storage.SkillBarCollection.Remove(GetNameConst());
            Storage.SellCollection.Remove(GetNameConst());
            Storage.LootCollection.Remove(GetNameConst());
        }

        public bool IsStarted()
        {
            return _Started;
        }

        private void ClientEvent()
        {
            try
            {
                while (true)
                {
                    if (GetProcessId() > 0 && HasExited())
                        return;

                    if (GetPhase() != EPhase.Disconnected && Read4Byte(GetRecvPointer()) != _MailslotRecvHookPtr.ToInt32())
                        PatchMailslot();

                    if (GetPhase() == EPhase.None && GetAddressListSize() > 0)
                    {
                        if (GetName() != "")
                            SetPhase(EPhase.Playing);
                    }

                    if (GetPhase() > EPhase.Loggining && IsConnectionLost())
                    {
                        SetPhase(EPhase.Disconnected);

                        SetDisconnectTime(Environment.TickCount);
                    }
                    else
                    {
                        if (GetPhase() != EPhase.Disconnected && GetDisconnectTime() > 0)
                            SetDisconnectTime(0);
                    }

                    Thread.Sleep(1);
                };
            }
            catch (ThreadAbortException ex)
            {
                Console.WriteLine("ClientEventThread -> Thread is aborted and the code is "
                                                 + ex.ExceptionState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ClientEventThread -> " + ex.StackTrace);
            }
        }

        private void RecvPacketEvent()
        {
            try
            {
                while (true)
                {
                    if (GetProcessId() > 0 && HasExited())
                        return;

                    Int32 MailslotRecvMessageSize = 0; Int32 MailslotRecvMessageLeft = 0;

                    GetMailslotInfo(_MailslotRecvPtr, IntPtr.Zero, out MailslotRecvMessageSize, out MailslotRecvMessageLeft, IntPtr.Zero);

                    if (MailslotRecvMessageSize > 0)
                    {

                        byte[] MessageBuffer = new byte[MailslotRecvMessageSize];

                        do
                        {
                            Int32 MessageReadByte = 0;

                            ReadFile(_MailslotRecvPtr, MessageBuffer, MailslotRecvMessageSize, out MessageReadByte, IntPtr.Zero);

                            if (MessageReadByte > 0)
                                ProcessRecvPacketEvent(MessageBuffer);

                            MailslotRecvMessageLeft -= 1;

                        } while (MailslotRecvMessageLeft != 0);
                    }

                    Thread.Sleep(1);
                };
            }
            catch (ThreadAbortException ex)
            {
                Console.WriteLine("RecvPacketEventThread -> Thread is aborted and the code is "
                                                 + ex.ExceptionState);
                StartThread(RecvPacketEvent);
            }
            catch (Exception ex)
            {

                Console.WriteLine("RecvPacketEventThread -> " + ex.StackTrace);
                StartThread(RecvPacketEvent);
            }
        }

        private void SendPacketEvent()
        {
            try
            {
                while (true)
                {
                    if (GetProcessId() > 0 && HasExited())
                        return;

                    Int32 MailslotSendMessageSize = 0; Int32 MailslotSendMessageLeft = 0;

                    GetMailslotInfo(_MailslotSendPtr, IntPtr.Zero, out MailslotSendMessageSize, out MailslotSendMessageLeft, IntPtr.Zero);

                    if (MailslotSendMessageSize > 0)
                    {
                        byte[] MessageBuffer = new byte[MailslotSendMessageSize];

                        do
                        {
                            Int32 MessageReadByte = 0;

                            ReadFile(_MailslotSendPtr, MessageBuffer, MailslotSendMessageSize, out MessageReadByte, IntPtr.Zero);

                            if (MessageReadByte > 0)
                                ProcessSendPacketEvent(MessageBuffer);

                            MailslotSendMessageLeft -= 1;

                        } while (MailslotSendMessageLeft != 0);
                    }

                    Thread.Sleep(1);
                };
            }
            catch (ThreadAbortException ex)
            {
                Console.WriteLine("SendPacketEventThread -> Thread is aborted and the code is "
                                                 + ex.ExceptionState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("SendPacketEventThread -> " + ex.StackTrace);
            }
        }

        private void CharacterEvent()
        {
            try
            {
                while (true)
                {
                    if (HasExited())
                        return;

                    if (IsCharacterAvailable() == false)
                    {
                        Thread.Sleep(1250);
                        continue;
                    }

                    if (Convert.ToBoolean(GetControl("SpeedHack")) == true && GetAsyncKeyState(System.Windows.Forms.Keys.G) != 0)
                    {
                        if (GetTargetId() > 0)
                            SetCoordinate(GetTargetX(), GetTargetY());
                        else
                        {
                            if (CoordinateDistance(GetX(), GetY(), GetGoX(), GetGoY()) <= 300)
                                SetCoordinate(GetGoX(), GetGoY());
                        }
                    }

                    if (Convert.ToBoolean(GetControl("Suicide")) == true)
                    {
                        double SuicidePercent = Math.Round(((double)GetHp() * 100) / GetMaxHp());

                        if (SuicidePercent <= Convert.ToInt32(GetControl("SuicidePercent")))
                        {
                            SetEnterGameTime(Environment.TickCount);

                            SendPacket("290103");
                            SendPacket("1200");
                        }
                    }

                    if (_PartyRequestTime > 0 && Environment.TickCount - _PartyRequestTime > 3000)
                    {
                        if(GetPartyCount() == 0)
                            SendPacket("2F0201");

                        _PartyRequestTime = 0;
                    }

                    if (Convert.ToBoolean(GetControl("BlackMarketer")) == true && _BlackMarketerEventTime % Convert.ToInt32(GetControl("BlackMarketerEventTime")) == 0)
                    {
                        SendPacket("20018E38FFFFFFFF");
                        Thread.Sleep(10);
                        SendPacket("55000D32353032325F425F4D2E6C7561");
                        Thread.Sleep(10);
                        SendPacket("55000D32353032325F425F4D2E6C7561");
                        Thread.Sleep(10);
                        SendPacket("55000D32353032325F425F4D2E6C7561");
                        Thread.Sleep(10);
                        SendPacket("55000D32353032325F425F4D2E6C7561");
                    }

                    _BlackMarketerEventTime = Environment.TickCount;

                    if (_CheatEventTime % 250 == 0)
                    {
                        if (Convert.ToBoolean(GetControl("DeathOnBorn")) == true)
                        {
                            if (GetHp() == 0)
                                SendPacket("1200");
                        }
                    }

                    if (_CheatEventTime % 1250 == 0)
                    {
                        if (Convert.ToBoolean(GetControl("Wallhack")) == true)
                        {
                            if (IsWallhackOn() == false)
                                Wallhack(true);
                        }
                        else
                        {
                            if (IsWallhackOn() == true)
                                Wallhack(false);
                        }

                        if (Convert.ToBoolean(GetControl("Oreads")) == true)
                        {
                            if (IsOreadsOn() == false)
                                Oreads(true);
                        }
                        else
                        {
                            if (IsOreadsOn() == true)
                                Oreads(false);
                        }
                    }

                    _CheatEventTime = Environment.TickCount;

                    Thread.Sleep(1);
                };
            }
            catch (ThreadAbortException ex)
            {
                Console.WriteLine("CharacterEventThread -> Thread is aborted and the code is "
                                                 + ex.ExceptionState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("CharacterEventThread -> " + ex.StackTrace);
            }
        }

        private void ProtectionEvent()
        {
            try
            {
                while (true)
                {
                    if (HasExited())
                        return;

                    if (IsCharacterAvailable() == false)
                    {
                        Thread.Sleep(1250);
                        continue;
                    }

                    if (_ProtectionEventTime % 30 == 0)
                    {
                        if (Convert.ToBoolean(GetControl("Minor")) == true)
                        {
                            double MinorPercent = Math.Round(((double)GetHp() * 100) / GetMaxHp());

                            if (MinorPercent <= Convert.ToInt32(GetControl("MinorPercent")))
                                UseMinorHealing(GetId());
                        }
                    }

                    if (_ProtectionEventTime % 250 == 0)
                    {
                        if (IsInEnterGame() == false && Convert.ToBoolean(GetControl("AreaControl")) == true && GetAction() == EAction.None)
                        {
                            if (Convert.ToBoolean(GetControl("FollowDisable")) == true || (Convert.ToBoolean(GetControl("FollowDisable")) == false && IsFollowOwner() == true))
                            {
                                if (Convert.ToInt32(GetControl("AreaControlX")) > 0 && Convert.ToInt32(GetControl("AreaControlY")) > 0
                                && CoordinateDistance(GetX(), GetY(), Convert.ToInt32(GetControl("AreaControlX")), Convert.ToInt32(GetControl("AreaControlY"))) > Convert.ToInt32(GetControl("AttackDistance")))
                                {
                                    SetCoordinate(Convert.ToInt32(GetControl("AreaControlX")), Convert.ToInt32(GetControl("AreaControlY")));
                                    Thread.Sleep(1250);
                                }
                            }
                        }
                    }

                    if (_ProtectionEventTime % 1250 == 0)
                    {
                        if (IsInEnterGame() == false && Convert.ToBoolean(GetControl("Transformation")) == true && IsTransformationAvailableZone())
                        {
                            int TransformationId = 0;

                            switch (GetControl("TransformationName"))
                            {
                                case "Kecoon":
                                    TransformationId = 472020;
                                    break;
                                case "Orc Bowman":
                                    TransformationId = 472310;
                                    break;
                                case "Death Knight":
                                    TransformationId = 472150;
                                    break;
                                case "Burning Skeleton":
                                    TransformationId = 472202;
                                    break;
                                case "Bulture":
                                    TransformationId = 472040;
                                    break;
                                case "Human Hera (L) 1":
                                    TransformationId = 520530;
                                    break;
                                case "Human Hera (L) 2":
                                    TransformationId = 520531;
                                    break;
                                case "Karus Hera (PUS)":
                                    TransformationId = 500512;
                                    break;
                                case "Karus Cougar (PUS)":
                                    TransformationId = 500511;
                                    break;
                                case "Karus Menicia (PUS)":
                                    TransformationId = 500510;
                                    break;
                                case "Karus Patrick (PUS)":
                                    TransformationId = 500509;
                                    break;
                                case "Human Hera (PUS)":
                                    TransformationId = 500508;
                                    break;
                                case "Human Cougar (PUS)":
                                    TransformationId = 500507;
                                    break;
                                case "Human Menicia (PUS)":
                                    TransformationId = 500506;
                                    break;
                                case "Human Patrick (PUS)":
                                    TransformationId = 500505;
                                    break;
                            }

                            if (IsInventoryItemExist(379091000) && IsSkillAffected(TransformationId) == false)
                            {
                                SendPacket("3103" +
                                 AlignDWORD(TransformationId).Substring(0, 6) +
                                 "00" +
                                 AlignDWORD(GetId()).Substring(0, 4) +
                                 AlignDWORD(GetId()).Substring(0, 4) +
                                 "000000000000000000000000");
                            }

                        }
                    }

                    if (_ProtectionEventTime % 2250 == 0)
                    {
                        if (Convert.ToBoolean(GetControl("AreaHeal")) == true)
                        {
                            if (GetHp() != GetMaxHp() || (GetPartyCount() > 0 && IsPartyMemberNeedHeal()))
                            {
                                SendPacket("3103" +
                                  AlignDWORD(492060) +
                                  AlignDWORD(GetId()).Substring(0, 4) +
                                  "FFFF00000000000000000000000000000000");
                            }

                        }
                    }

                    if (_ProtectionEventTime % 2500 == 0)
                    {
                        double HpPotionPercent = Math.Round(((double)GetHp() * 100) / GetMaxHp());

                        if (Convert.ToBoolean(GetControl("HpPotion")) == true && HpPotionPercent <= Convert.ToInt32(GetControl("HpPotionPercent")))
                        {
                            int HpPotionItem = 0;

                            switch (GetControl("HpPotionItem"))
                            {
                                case "Water of favors":
                                    HpPotionItem = 389014000;
                                    break;
                                case "Water of grace":
                                    HpPotionItem = 389013000;
                                    break;
                                case "Water of love":
                                    HpPotionItem = 389012000;
                                    break;
                                case "Water of life":
                                    HpPotionItem = 389011000;
                                    break;
                                case "Holy water":
                                    HpPotionItem = 389010000;
                                    break;
                            }

                            if (IsInventoryItemExist(HpPotionItem))
                            {
                                SendPacket("3103" +
                                  AlignDWORD(490000 +
                                    Convert.ToInt32(HpPotionItem.ToString().Substring(4, 2))).Substring(0, 6) +
                                  "00" +
                                  AlignDWORD(GetId()).Substring(0, 4) +
                                  AlignDWORD(GetId()).Substring(0, 4) +
                                  "0000000000000000000000000000");
                            }
                        }

                        double MpPotionPercent = Math.Round(((double)GetMp() * 100) / GetMaxMp());

                        if (Convert.ToBoolean(GetControl("MpPotion")) == true && MpPotionPercent <= Convert.ToInt32(GetControl("MpPotionPercent")))
                        {
                            int MpPotionItem = 0;

                            switch (GetControl("MpPotionItem"))
                            {
                                case "Potion of Soul":
                                    MpPotionItem = 389020000;
                                    break;
                                case "Potion of Wisdom":
                                    MpPotionItem = 389019000;
                                    break;
                                case "Potion of Sagacity":
                                    MpPotionItem = 389018000;
                                    break;
                                case "Potion of Intelligence":
                                    MpPotionItem = 389017000;
                                    break;
                                case "Potion of Spirit":
                                    MpPotionItem = 389016000;
                                    break;
                            }

                            if (IsInventoryItemExist(MpPotionItem))
                                SendPacket("3103" +
                                  AlignDWORD(490000 +
                                    Convert.ToInt32(MpPotionItem.ToString().Substring(4, 2))).Substring(0, 6) +
                                  "00" +
                                  AlignDWORD(GetId()).Substring(0, 4) +
                                  AlignDWORD(GetId()).Substring(0, 4) +
                                  "0000000000000000000000000000");

                        }
                    }

                    if (_ProtectionEventTime % 3000 == 0)
                    {
                        if (Convert.ToBoolean(GetControl("StatScroll")) == true)
                        {
                            if (IsSkillAffected(492059) == false)
                                SendPacket("3103" +
                                  AlignDWORD(492059) +
                                  AlignDWORD(GetId()).Substring(0, 4) +
                                  "FFFF00000000000000000000000000000000");
                        }

                        if (Convert.ToBoolean(GetControl("AttackScroll")) == true)
                        {
                            if (IsSkillAffected(500271) == false)
                                SendPacket("3103" +
                                  AlignDWORD(500271) +
                                  AlignDWORD(GetId()).Substring(0, 4) +
                                  AlignDWORD(GetId()).Substring(0, 4) +
                                  "00000000000000000000000000000000");
                        }

                        if (Convert.ToBoolean(GetControl("AcScroll")) == true)
                        {
                            if (IsSkillAffected(492061) == false && IsSkillAffected(492024) == false)
                                SendPacket("3103" +
                                  AlignDWORD(492061) +
                                  AlignDWORD(GetId()).Substring(0, 4) +
                                  "FFFF00000000000000000000000000000000");
                        }

                        if (Convert.ToBoolean(GetControl("DropScroll")) == true)
                        {
                            if (IsSkillAffected(492024) == false && IsSkillAffected(492023) == false)
                                SendPacket("3103" +
                                  AlignDWORD(492023) +
                                  AlignDWORD(GetId()).Substring(0, 4) +
                                  AlignDWORD(GetId()).Substring(0, 4) +
                                  "00000000000000000000000000000000");

                            if (IsSkillAffected(492023) == false && IsSkillAffected(492024) == false && IsSkillAffected(492061) == false)
                                SendPacket("3103" +
                                  AlignDWORD(492024) +
                                  AlignDWORD(GetId()).Substring(0, 4) +
                                  AlignDWORD(GetId()).Substring(0, 4) +
                                  "00000000000000000000000000000000");
                        }
                    }

                    _ProtectionEventTime = Environment.TickCount;

                    Thread.Sleep(1);
                };
            }
            catch (ThreadAbortException ex)
            {
                Console.WriteLine("ProtectionEventTime -> Thread is aborted and the code is "
                                                 + ex.ExceptionState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProtectionEventTime -> " + ex.StackTrace);
            }
        }

        private void AttackEvent()
        {
            try
            {
                while (true)
                {
                    if (HasExited())
                        return;

                    if (IsCharacterAvailable() == false || GetAction() != EAction.None || IsInEnterGame())
                    {
                        Thread.Sleep(1250);
                        continue;
                    }

                    if(Convert.ToBoolean(GetControl("Attack")) == false)
                    {
                        Thread.Sleep(1250);
                        continue;
                    }

                    if (Convert.ToBoolean(GetControl("Attack")) == true && _AttackEventTime % Convert.ToInt32(GetControl("AttackSpeed")) == 0)
                    {
                        var SkillBarList = Database().GetSkillBarList(GetNameConst(), GetPlatform().ToString()).FindAll(x => x.SkillType == 1);

                        for (int i = 0; i < SkillBarList.Count; i++)
                        {
                            SkillBar SkillBarData = SkillBarList[i];

                            if (SkillBarData == null) continue;

                            Skill SkillData = Database().GetSkillData(GetNameConst(), (int)SkillBarData.SkillId);

                            if (SkillData == null) continue;

                            if (SkillData.Cooldown == 0 || Environment.TickCount > SkillBarData.UseTime + (SkillData.Cooldown * 1000))
                            {
                                if (UseSkill(SkillData, GetTargetId()))
                                {
                                    SkillBarData.UseTime = Environment.TickCount;

                                    Thread.Sleep(1000);
                                }
                            }

                        }
                    }

                    _AttackEventTime = Environment.TickCount;

                    Thread.Sleep(1);
                };
            }
            catch (ThreadAbortException ex)
            {
                Console.WriteLine("AttackEventThread -> Thread is aborted and the code is "
                                                 + ex.ExceptionState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("AttackEventThread -> " + ex.StackTrace);
            }
        }

        private void TimedSkillEvent()
        {
            try
            {
                while (true)
                {
                    if (HasExited())
                        return;

                    if (IsCharacterAvailable() == false || IsInEnterGame())
                    {
                        Thread.Sleep(1250);
                        continue;
                    }

                    if (_TimedSkillEventTime % 2250 == 0)
                    {
                        var SkillBarList = Database().GetSkillBarList(GetNameConst(), GetPlatform().ToString()).FindAll(x => x.SkillType == 2);

                        for (int i = 0; i < SkillBarList.Count; i++)
                        {
                            SkillBar SkillBarData = SkillBarList[i];

                            if (SkillBarData == null) continue;

                            Skill SkillData = Database().GetSkillData(GetNameConst(), (int)SkillBarData.SkillId);

                            if (SkillData == null) continue;

                            if (Convert.ToBoolean(GetControl("WaitTime")) == false || SkillData.Cooldown == 0 || Environment.TickCount > SkillBarData.UseTime + (SkillData.Cooldown * 1000))
                            {
                                if (UseSkill(SkillData))
                                {
                                    SkillBarData.UseTime = Environment.TickCount;

                                    Thread.Sleep(1500);
                                }
                            }
                        }
                    }

                    _TimedSkillEventTime = Environment.TickCount;

                    Thread.Sleep(1);
                };
            }
            catch (ThreadAbortException ex)
            {
                Console.WriteLine("AttackEventThread -> Thread is aborted and the code is "
                                                 + ex.ExceptionState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("AttackEventThread -> " + ex.StackTrace);
            }
        }

        private void PartyEvent()
        {
            try
            {
                while (true)
                {
                    if (HasExited())
                        return;

                    if (GetPlatform() != AddressEnum.Platform.JPKO)
                        return;

                    if (IsCharacterAvailable() == false || GetPartyCount() == 0 || GetJob(GetClass()) == "Warrior" || IsInEnterGame())
                    {
                        Thread.Sleep(1250);
                        continue;
                    }

                    List<Party> PartyList = new List<Party>();

                    if (GetPartyList(ref PartyList) > 0)
                    {
                        if (PartyList.Count != _PartyCollection.Count)
                            _PartyCollection = PartyList;

                        PartyList.ForEach(x =>
                        {
                            Party OldMemberData = _PartyCollection.Find(y => y.MemberId == x.MemberId);

                            if (OldMemberData != null)
                            {
                                switch (GetJob(GetClass()))
                                {
                                    case "Priest":
                                        {
                                            if (GetPartyAllowedSize() == 0 || (GetPartyAllowedSize() > 0 && GetPartyAllowed(x.MemberName)))
                                            {
                                                if (Convert.ToBoolean(GetControl("PartyBuff")) && OldMemberData.MemberBuffHp != x.MemberMaxHp)
                                                {
                                                    string SelectedHealthBuff = GetControl("PartyBuffSelect");

                                                    if (SelectedHealthBuff == "Otomatik")
                                                        SelectedHealthBuff = GetProperHealthBuff(x.MemberMaxHp);

                                                    Skill SkillData = Database().GetSkillData(GetNameConst(), SelectedHealthBuff);

                                                    if (SkillData != null)
                                                        UsePriestSkill(SkillData, x.MemberId);
                                                }

                                                if (Convert.ToBoolean(GetControl("PartyAc")) && OldMemberData.MemberBuffHp != x.MemberMaxHp)
                                                {
                                                    string SelectedDefenseBuff = GetControl("PartyAcSelect");

                                                    if (SelectedDefenseBuff == "Otomatik")
                                                        SelectedDefenseBuff = GetProperDefenseBuff();

                                                    Skill SkillData = Database().GetSkillData(GetNameConst(), SelectedDefenseBuff);

                                                    if (SkillData != null)
                                                        UsePriestSkill(SkillData, x.MemberId);

                                                }

                                                if (Convert.ToBoolean(GetControl("PartyMind")) && OldMemberData.MemberBuffHp != x.MemberMaxHp)
                                                {
                                                    string SelectedMindBuff = GetControl("PartyMindSelect");

                                                    if (SelectedMindBuff == "Otomatik")
                                                        SelectedMindBuff = GetProperMindBuff();

                                                    Skill SkillData = Database().GetSkillData(GetNameConst(), SelectedMindBuff);

                                                    if (SkillData != null)
                                                        UsePriestSkill(SkillData, x.MemberId);

                                                }

                                                if (Convert.ToBoolean(GetControl("PartyStr")) && GetJob(x.MemberClass) == "Warrior" && OldMemberData.MemberBuffHp != x.MemberMaxHp)
                                                {
                                                    Skill SkillData = Database().GetSkillData(GetNameConst(), "Strength");

                                                    if (SkillData != null)
                                                        UsePriestSkill(SkillData, x.MemberId);

                                                }

                                                if (Convert.ToBoolean(GetControl("PartyCure")))
                                                {
                                                    if (x.MemberCure1 == 256)
                                                    {
                                                        Skill SkillData = Database().GetSkillData(GetNameConst(), "Cure Curse");

                                                        if (SkillData != null)
                                                            UsePriestSkill(SkillData, x.MemberId);
                                                    }
                                                }

                                                if (Convert.ToBoolean(GetControl("PartyCureDisease")))
                                                {
                                                    if (x.MemberCure1 == 257 || x.MemberCure1 == 1 || x.MemberCure1 == 65536)
                                                    {
                                                        Skill SkillData = Database().GetSkillData(GetNameConst(), "Cure Disease");

                                                        if (SkillData != null)
                                                            UsePriestSkill(SkillData, x.MemberId);
                                                    }
                                                }

                                                OldMemberData.MemberBuffHp = x.MemberMaxHp;

                                                Thread.Sleep(1250);
                                            }
                                        }
                                        break;

                                    case "Rogue":
                                        {
                                            if (GetPartyAllowedSize() == 0 || (GetPartyAllowedSize() > 0 && GetPartyAllowed(x.MemberName)))
                                            {
                                                if (Convert.ToBoolean(GetControl("PartyMinor")))
                                                {
                                                    double MinorPercent = Math.Round(((double)x.MemberHp * 100) / x.MemberMaxHp);

                                                    if (MinorPercent <= Convert.ToInt32(GetControl("PartyMinorPercent")))
                                                        UseMinorHealing(x.MemberId);

                                                    Thread.Sleep(30);
                                                }

                                            }
                                        }
                                        break;

                                    case "Mage":
                                        {
                                            if (GetPartyAllowedSize() == 0 || (GetPartyAllowedSize() > 0 && GetPartyAllowed(x.MemberName)))
                                            {
                                                if (Convert.ToBoolean(GetControl("PullAway")) && x.MemberId != GetId())
                                                {
                                                    int MemberBase = GetPlayerBase(x.MemberId);

                                                    int MemberX = (int)Math.Round(ReadFloat(MemberBase + GetAddress("KO_OFF_X")));
                                                    int MemberY = (int)Math.Round(ReadFloat(MemberBase + GetAddress("KO_OFF_Y")));

                                                    int MemberDistance = CoordinateDistance(GetX(), GetY(), MemberX, MemberY);

                                                    if (MemberBase == 0 || (MemberBase > 0 && MemberDistance > 50))
                                                    {
                                                        Skill SkillData = Database().GetSkillData(GetNameConst(), "Summon Friend");

                                                        if (SkillData != null)
                                                            UseMageSkill(SkillData, x.MemberId);

                                                        Thread.Sleep(1250);
                                                    }
                                                }

                                                if (Convert.ToBoolean(GetControl("LightningResist")) && Environment.TickCount > OldMemberData.MemberResistTime + (305 * 1000))
                                                {
                                                    int MemberBase = GetPlayerBase(x.MemberId);

                                                    if (MemberBase > 0 || x.MemberId == GetId())
                                                    {
                                                        int MemberX = (int)Math.Round(ReadFloat(MemberBase + GetAddress("KO_OFF_X")));
                                                        int MemberY = (int)Math.Round(ReadFloat(MemberBase + GetAddress("KO_OFF_Y")));

                                                        int MemberDistance = CoordinateDistance(GetX(), GetY(), MemberX, MemberY);

                                                        if (MemberDistance < 30 || x.MemberId == GetId())
                                                        {
                                                            Skill SkillData = Database().GetSkillData(GetNameConst(), "Immunity Lightning");

                                                            if (SkillData != null)
                                                            {
                                                                if (UseMageSkill(SkillData, x.MemberId))
                                                                    OldMemberData.MemberResistTime = Environment.TickCount;
                                                            }

                                                            Thread.Sleep(1250);
                                                        }
                                                    }
                                                }

                                                if (Convert.ToBoolean(GetControl("FlameResist")) && Environment.TickCount > OldMemberData.MemberResistTime + (305 * 1000))
                                                {
                                                    int MemberBase = GetPlayerBase(x.MemberId);

                                                    if (MemberBase > 0 || x.MemberId == GetId())
                                                    {
                                                        int MemberX = (int)Math.Round(ReadFloat(MemberBase + GetAddress("KO_OFF_X")));
                                                        int MemberY = (int)Math.Round(ReadFloat(MemberBase + GetAddress("KO_OFF_Y")));

                                                        int MemberDistance = CoordinateDistance(GetX(), GetY(), MemberX, MemberY);

                                                        if (MemberDistance < 30 || x.MemberId == GetId())
                                                        {
                                                            Skill SkillData = Database().GetSkillData(GetNameConst(), "Immunity Fire");

                                                            if (SkillData != null)
                                                            {
                                                                if (UseMageSkill(SkillData, x.MemberId))
                                                                    OldMemberData.MemberResistTime = Environment.TickCount;
                                                            }

                                                            Thread.Sleep(1250);
                                                        }
                                                    }
                                                }

                                                if (Convert.ToBoolean(GetControl("GlacierResist")) && Environment.TickCount > OldMemberData.MemberResistTime + (305 * 1000))
                                                {
                                                    int MemberBase = GetPlayerBase(x.MemberId);

                                                    if (MemberBase > 0 || x.MemberId == GetId())
                                                    {
                                                        int MemberX = (int)Math.Round(ReadFloat(MemberBase + GetAddress("KO_OFF_X")));
                                                        int MemberY = (int)Math.Round(ReadFloat(MemberBase + GetAddress("KO_OFF_Y")));

                                                        int MemberDistance = CoordinateDistance(GetX(), GetY(), MemberX, MemberY);

                                                        if (MemberDistance < 30 || x.MemberId == GetId())
                                                        {
                                                            Skill SkillData = Database().GetSkillData(GetNameConst(), "Immunity Cold");

                                                            if (SkillData != null)
                                                            {
                                                                if (UseMageSkill(SkillData, x.MemberId))
                                                                    OldMemberData.MemberResistTime = Environment.TickCount;
                                                            }

                                                            Thread.Sleep(1250);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        break;
                                }

                            }
                        });
                    }

                    Thread.Sleep(150);
                };
            }
            catch (ThreadAbortException ex)
            {
                Console.WriteLine("PartyEvent -> Thread is aborted and the code is "
                                                 + ex.ExceptionState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("PartyEvent -> " + ex.StackTrace);
            }
        }

        private bool HasAnyRepairing()
        {
            if (Storage.FollowedClient == null) return GetAction() == EAction.Repairing;

            foreach (Client ClientData in Storage.ClientCollection.Values.ToList())
            {
                if (ClientData == null) continue;
                if (ClientData.GetAction() == EAction.Repairing) return true;
            }

            return false;
        }

        private void RepairEvent()
        {
            try
            {
                while (true)
                {
                    if (HasExited())
                        return;

                    if (IsCharacterAvailable() == false || HasAnyRepairing() || HasAnySupplying() || IsInEnterGame())
                    {
                        Thread.Sleep(1250);
                        continue;
                    }

                    if (_RepairEventTime % 1250 == 0 && Environment.TickCount - _RepairEventAfterWaitTime > 60000)
                    {
                        if (IsNeedRepair() && Convert.ToBoolean(GetControl("RepairSunderies")) == true && Convert.ToBoolean(GetControl("RepairMagicHammer")) == false)
                            RepairEquipmentAction();

                        if (IsNeedRepair() && Convert.ToBoolean(GetControl("RepairSunderies")) == false && Convert.ToBoolean(GetControl("RepairMagicHammer")) == true)
                            SendPacket("3103" + AlignDWORD(490202) + AlignDWORD(GetId()).Substring(0, 4) + AlignDWORD(GetId()).Substring(0, 4) + "00000000000000000000000000000000");
                    }

                    _RepairEventTime = Environment.TickCount;

                    Thread.Sleep(1);
                };
            }
            catch (ThreadAbortException ex)
            {
                Console.WriteLine("RepairEvent -> Thread is aborted and the code is "
                                                 + ex.ExceptionState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("RepairEvent -> " + ex.StackTrace);
            }
        }

        private bool HasAnySupplying()
        {
            if (Storage.FollowedClient == null) return GetAction() == EAction.Supplying;

            foreach (Client ClientData in Storage.ClientCollection.Values.ToList())
            {
                if (ClientData == null) continue;
                if (ClientData.GetAction() == EAction.Supplying) return true;
            }

            return false;
        }

        private void SupplyEvent()
        {
            try
            {
                while (true)
                {
                    if (HasExited())
                        return;

                    if (IsCharacterAvailable() == false || HasAnyRepairing() || HasAnySupplying() || IsInEnterGame())
                    {
                        Thread.Sleep(1250);
                        continue;
                    }

                    if (_SupplyEventTime % 1250 == 0 && Environment.TickCount - _SupplyEventAfterWaitTime > 60000)
                    {
                        List<Supply> Supply = new List<Supply>();
                        if (IsNeedSupply(ref Supply))
                            SupplyItemAction(Supply);
                    }

                    _SupplyEventTime = Environment.TickCount;

                    Thread.Sleep(1);
                };
            }
            catch (ThreadAbortException ex)
            {
                Console.WriteLine("SupplyEvent -> Thread is aborted and the code is "
                                                 + ex.ExceptionState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("SupplyEvent -> " + ex.StackTrace);
            }
        }

        public bool IsFollowOwner()
        {
            return Storage.FollowedClient != null && Storage.FollowedClient.GetProcessId() == GetProcessId();
        }

        private void TargetAndActionEvent()
        {
            try
            {
                while (true)
                {
                    if (HasExited())
                        return;

                    if (IsCharacterAvailable() == false || GetAction() != EAction.None || IsInEnterGame())
                    {
                        Thread.Sleep(1250);
                        continue;
                    }

                    if (Convert.ToBoolean(GetControl("FollowDisable")) == false && IsFollowOwner() == false)
                    {
                        Thread.Sleep(1250);
                        continue;
                    }

                    if (_TargetEventTime % 250 == 0)
                    {
                        if (Convert.ToBoolean(GetControl("TargetAutoSelect")) == true || GetTargetAllowedSize() > 0)
                        {
                            if ((Storage.FollowedClient != null && Storage.FollowedClient.GetProcessId() == GetProcessId())
                                || (Convert.ToBoolean(GetControl("FollowDisable")) == true))
                            {
                                int TargetId = GetTargetId();

                                if (TargetId > 0)
                                {
                                    if (Convert.ToBoolean(GetControl("TargetWaitDown")) == true)
                                    {
                                        if (IsAttackableTarget(TargetId) == false)
                                        {
                                            Thread.Sleep(500);
                                            SelectTarget(0, 500);
                                        }
                                    }
                                    else
                                    {
                                        if (IsAttackableTarget(TargetId) == false)
                                            SelectTarget(0);
                                    }
                                }
                                else
                                {
                                    List<Target> TargetList = new List<Target>();
                                    SearchMob(ref TargetList);

                                    if (TargetList.Count > 0)
                                    {
                                        if (GetTargetAllowedSize() > 0)
                                        {
                                            if (Convert.ToBoolean(GetControl("TargetOpponentNation")) == true)
                                                SearchPlayer(ref TargetList);

                                            Target Target = TargetList
                                                .FindAll(x => IsAttackableTarget(x.Id)
                                                            && GetTargetAllowed(x.Name)
                                                            && CoordinateDistance(x.X, x.Y, GetX(), GetY()) < Convert.ToInt32(GetControl("AttackDistance")))
                                                .GroupBy(x => Math.Pow((GetX() - x.X), 2) + Math.Pow((GetY() - x.Y), 2))
                                                .OrderBy(x => x.Key)
                                                ?.FirstOrDefault()
                                                ?.FirstOrDefault();

                                            if (Target != null)
                                                SelectTarget(Read4Byte(Target.Base + GetAddress("KO_OFF_ID")));
                                        }
                                        else if (Convert.ToBoolean(GetControl("TargetOpponentNation")) == true)
                                        {
                                            SearchPlayer(ref TargetList);

                                            Target Target = TargetList
                                                .FindAll(x => IsAttackableTarget(x.Id)
                                                            && (Convert.ToBoolean(GetControl("TargetOpponentNation")) == true && x.Nation != 3 && x.Nation != GetNation())
                                                            && CoordinateDistance(x.X, x.Y, GetX(), GetY()) < Convert.ToInt32(GetControl("AttackDistance")))
                                                .GroupBy(x => Math.Pow((GetX() - x.X), 2) + Math.Pow((GetY() - x.Y), 2))
                                                .OrderBy(x => x.Key)
                                                ?.FirstOrDefault()
                                                ?.FirstOrDefault();

                                            if (Target != null)
                                                SelectTarget(Read4Byte(Target.Base + GetAddress("KO_OFF_ID")));
                                        }
                                        else
                                        {
                                            Target Target = TargetList
                                                .FindAll(x => IsAttackableTarget(x.Id)
                                                            && x.Nation == 0
                                                            && CoordinateDistance(x.X, x.Y, GetX(), GetY()) < Convert.ToInt32(GetControl("AttackDistance")))
                                                .GroupBy(x => Math.Pow((GetX() - x.X), 2) + Math.Pow((GetY() - x.Y), 2))
                                                .OrderBy(x => x.Key)
                                                ?.FirstOrDefault()
                                                ?.FirstOrDefault();

                                            if (Target != null)
                                                SelectTarget(Read4Byte(Target.Base + GetAddress("KO_OFF_ID")));
                                        }
                                    }
                                }
                            }
                        }
                    }

                    _TargetEventTime = Environment.TickCount;

                    if (_ActionEventTime % 250 == 0)
                    {
                        if ((Storage.FollowedClient != null && Storage.FollowedClient.GetProcessId() == GetProcessId())
                            || (Convert.ToBoolean(GetControl("FollowDisable")) == true))
                        {
                            int TargetX = GetTargetX(); int TargetY = GetTargetY();
                            if (GetTargetId() > 0 && (TargetX != GetX() || TargetY != GetY()))
                            {
                                if (Convert.ToBoolean(GetControl("ActionMove")) == true)
                                    MoveCoordinate(TargetX, TargetY);
                                else if (Convert.ToBoolean(GetControl("ActionSetCoordinate")) == true)
                                    SetCoordinate(TargetX, TargetY);
                            }
                        }
                    }

                    _ActionEventTime = Environment.TickCount;

                    Thread.Sleep(1);
                };
            }
            catch (ThreadAbortException ex)
            {
                Console.WriteLine("TargetEvent -> Thread is aborted and the code is "
                                                 + ex.ExceptionState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("TargetEvent -> " + ex.StackTrace);
            }
        }

        private void MobClearEvent()
        {
            try
            {
                while (true)
                {
                    if (HasExited())
                        return;

                    if (IsCharacterAvailable() == false || GetAction() != EAction.None || IsInEnterGame())
                    {
                        Thread.Sleep(1250);
                        continue;
                    }

                    if(Convert.ToBoolean(GetControl("RemoveAllMob")) == false)
                    {
                        Thread.Sleep(1250);
                        continue;
                    }

                    ClearAllMob();

                    Thread.Sleep(1);
                };
            }
            catch (ThreadAbortException ex)
            {
                Console.WriteLine("MobClearEvent -> Thread is aborted and the code is "
                                                 + ex.ExceptionState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("MobClearEvent -> " + ex.StackTrace);
            }
        }

        private void MiningEvent()
        {
            try
            {
                while (true)
                {
                    if (HasExited())
                        return;

                    if (IsCharacterAvailable() == false || GetAction() != EAction.None || IsInEnterGame())
                    {
                        Thread.Sleep(1250);
                        continue;
                    }

                    if(Convert.ToBoolean(GetControl("MiningEnable")) == false || (GetZone() != 1 && GetZone() != 2))
                    {
                        Thread.Sleep(1250);
                        continue;
                    }

                    if (_MiningEventTime % 200 == 0)
                    {
                        if (Convert.ToBoolean(GetControl("MiningFullExchange")) == true)
                        {
                            Npc Miner = Storage.NpcCollection
                                            .FindAll(x => x.Type == "Miner" && x.Zone == GetZone() && (x.Nation == 0 || x.Nation == GetNation()))
                                            .GroupBy(x => Math.Pow((GetX() - x.X), 2) + Math.Pow((GetY() - x.Y), 2))
                                            .OrderBy(x => x.Key)
                                            ?.FirstOrDefault()
                                            ?.FirstOrDefault();

                            int OreCount = GetInventoryItemCount(399210000);

                            if (Convert.ToBoolean(GetControl("GoldenMattock")) == true)
                                OreCount = GetInventoryItemCount(399200000);

                            if (Miner != null && OreCount > 0 && GetInventoryAvailableSlotCount() <= 2)
                            {
                                SendNotice("Maden kırdırma işlemi başladı.");

                                SetAction(EAction.MineExchanging);

                                Thread.Sleep(1250);

                                int iLastX = GetX(); int iLastY = GetY();

                                SetCoordinate((int)Miner.X, (int)Miner.Y, 2500);

                                while (GetAction() == EAction.MineExchanging)
                                {
                                    if (CoordinateDistance(GetX(), GetY(), (int)Miner.X, (int)Miner.Y) > 5)
                                        SetCoordinate((int)Miner.X, (int)Miner.Y, 2500);
                                    else
                                    {
                                        if (Convert.ToBoolean(GetControl("MiningRemoveTrashItem")) == true)
                                            RemoveAllMiningTrashItem();

                                        Thread.Sleep(1250);

                                        for (int i = 0; i < OreCount; i++)
                                        {
                                            SendPacket("2001" + AlignDWORD(Miner.RealId).Substring(0, 4) + "FFFFFFFF");
                                            Thread.Sleep(10);
                                            SendPacket("6407544E0000");
                                            Thread.Sleep(10);
                                            SendPacket("55001033313531315F5069746D616E2E6C7561");
                                            Thread.Sleep(10);
                                            SendPacket("55001033313531315F5069746D616E2E6C7561");
                                            Thread.Sleep(10);
                                            SendPacket("22A92D00313531315F5069746D616E2E6C7561");
                                            Thread.Sleep(500);
                                        }

                                        Thread.Sleep(2500);

                                        SetCoordinate((int)iLastX, (int)iLastY, 2500);

                                        while (GetAction() == EAction.MineExchanging)
                                        {
                                            if (CoordinateDistance(GetX(), GetY(), (int)iLastX, (int)iLastY) > 5)
                                                SetCoordinate((int)iLastX, (int)iLastY, 2500);
                                            else
                                                SetAction(EAction.None);
                                        }
                                    }
                                }

                                SendNotice("Maden kırdırma işlemi sona erdi.");
                            }
                        }
                    }

                    if (_MiningEventTime % 1250 == 0)
                    {
                        if (IsMining() == false)
                        {
                            if (GetZone() == 1)
                            {
                                if (CoordinateDistance(GetX(), GetY(), 654, 1673) > 10)
                                    SetCoordinate(654, 1673, 2500);
                                else
                                    SendPacket("8601");
                            }

                            if (GetZone() == 2)
                            {
                                if (CoordinateDistance(GetX(), GetY(), 1702, 572) > 10)
                                    SetCoordinate(1702, 572, 2500);
                                else
                                    SendPacket("8601");
                            }
                        }
                    }

                    _MiningEventTime = Environment.TickCount;

                    Thread.Sleep(1);
                };
            }
            catch (ThreadAbortException ex)
            {
                Console.WriteLine("MiningEvent -> Thread is aborted and the code is "
                                                 + ex.ExceptionState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("MiningEvent -> " + ex.StackTrace);
            }
        }

        private void MonsterStoneEvent()
        {
            try
            {
                while (true)
                {
                    if (HasExited())
                        return;

                    if (IsCharacterAvailable() == false || GetAction() != EAction.None || IsInEnterGame())
                    {
                        Thread.Sleep(1250);
                        continue;
                    }

                    if (Convert.ToBoolean(GetControl("MonsterStoneEnable")) == false)
                    {
                        Thread.Sleep(1250);
                        continue;
                    }

                    if (IsInMonsterStoneZone())
                    {
                        if (CoordinateDistance(GetX(), GetY(), 57, 57) > 75)
                            SetCoordinate(57, 57);
                    }
                    else
                    {
                        if (_MonsterStoneEventTime % 1250 == 0)
                        {
                            int InventoryEmptySlotCount = GetInventoryAvailableSlotCount();

                            if (InventoryEmptySlotCount >= 2)
                                SendPacket("5F06971BA735");
                        }
                    }

                    _MonsterStoneEventTime = Environment.TickCount;

                    Thread.Sleep(1);
                };
            }
            catch (ThreadAbortException ex)
            {
                Console.WriteLine("MonsterStoneEvent -> Thread is aborted and the code is "
                                                 + ex.ExceptionState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("MonsterStoneEvent -> " + ex.StackTrace);
            }
        }

        private void AutoLootEvent()
        {
            try
            {
                while (true)
                {
                    if (HasExited())
                        return;

                    if (IsCharacterAvailable() == false || GetAction() != EAction.None || IsInEnterGame())
                    {
                        Thread.Sleep(1250);
                        continue;
                    }

                    if (Convert.ToBoolean(GetControl("OnlyNoah")) || Convert.ToBoolean(GetControl("LootOnlyList")) || Convert.ToBoolean(GetControl("LootOnlySell")))
                    {
                        var AutoLootPool = _AutoLootCollection.ToList();

                        foreach (var LootData in AutoLootPool)
                        {
                            if (LootData == null) continue;
                            if ((Environment.TickCount - LootData.DropTime) > 20000)
                                _AutoLootCollection.RemoveAll(x => x.Id == LootData.Id);
                            else
                            {
                                if ((Environment.TickCount - LootData.DropTime) > 700)
                                    SendPacket("24" + LootData.Id);
                            }
                        }
                    }

                    Thread.Sleep(1);
                };
            }
            catch (ThreadAbortException ex)
            {
                Console.WriteLine("MonsterStoneEvent -> Thread is aborted and the code is "
                                                 + ex.ExceptionState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("MonsterStoneEvent -> " + ex.StackTrace);
            }
        }
    }
}