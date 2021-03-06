﻿#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 LastPosition.cs is part of SFXUtility.

 SFXUtility is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXUtility is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXUtility. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

namespace SFXUtility.Features.Trackers
{
    #region

    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using Classes;
    using Detectors;
    using LeagueSharp;
    using LeagueSharp.Common;
    using Properties;
    using SFXLibrary;
    using SFXLibrary.Extensions.NET;
    using SFXLibrary.Logger;
    using SharpDX;
    using Color = SharpDX.Color;

    #endregion

    internal class LastPosition : Base
    {
        private readonly List<LastPositionObject> _lastPositionObjects = new List<LastPositionObject>();
        private Trackers _parent;

        public override bool Enabled
        {
            get { return _parent != null && _parent.Enabled && Menu != null && Menu.Item(Name + "Enabled").GetValue<bool>(); }
        }

        public override string Name
        {
            get { return Language.Get("F_LastPosition"); }
        }

        protected override void OnGameLoad(EventArgs args)
        {
            try
            {
                if (Global.IoC.IsRegistered<Trackers>())
                {
                    _parent = Global.IoC.Resolve<Trackers>();
                    if (_parent.Initialized)
                        OnParentInitialized(null, null);
                    else
                        _parent.OnInitialized += OnParentInitialized;
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void TeleportAbort(object sender, TeleportEventArgs teleportEventArgs)
        {
            var lastPosition = _lastPositionObjects.FirstOrDefault(e => e.Hero.NetworkId == teleportEventArgs.UnitNetworkId);
            if (lastPosition != null)
            {
                lastPosition.IsTeleporting = false;
            }
        }

        private void TeleportEnabled(object sender, EventArgs eventArgs)
        {
            _lastPositionObjects.ForEach(e => e.Teleport = true);
        }

        private void TeleportDisabled(object sender, EventArgs eventArgs)
        {
            _lastPositionObjects.ForEach(e => e.Teleport = false);
        }

        private void TeleportFinish(object sender, TeleportEventArgs teleportEventArgs)
        {
            var lastPosition = _lastPositionObjects.FirstOrDefault(e => e.Hero.NetworkId == teleportEventArgs.UnitNetworkId);
            if (lastPosition != null)
            {
                lastPosition.Teleported = true;
                lastPosition.IsTeleporting = false;
            }
        }

        private void TeleportStart(object sender, TeleportEventArgs teleportEventArgs)
        {
            var lastPosition = _lastPositionObjects.FirstOrDefault(e => e.Hero.NetworkId == teleportEventArgs.UnitNetworkId);
            if (lastPosition != null)
            {
                lastPosition.IsTeleporting = true;
            }
        }

        protected override void OnEnable()
        {
            _lastPositionObjects.ForEach(enemy => enemy.Active = true);
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            _lastPositionObjects.ForEach(enemy => enemy.Active = false);
            base.OnDisable();
        }

        private void OnParentInitialized(object sender, EventArgs eventArgs)
        {
            try
            {
                if (_parent.Menu == null)
                    return;

                Menu = new Menu(Name, Name);

                var drawingMenu = new Menu(Language.Get("G_Drawing"), Name + "Drawing");
                drawingMenu.AddItem(
                    new MenuItem(drawingMenu.Name + "TimeFormat", Language.Get("G_TimeFormat")).SetValue(new StringList(new[] {"mm:ss", "ss"})));
                drawingMenu.AddItem(new MenuItem(drawingMenu.Name + "FontSize", Language.Get("G_FontSize")).SetValue(new Slider(13, 3, 30)));
                drawingMenu.AddItem(
                    new MenuItem(drawingMenu.Name + "SSTimerOffset", Language.Get("LastPosition_SSTimer") + " " + Language.Get("G_Offset")).SetValue(
                        new Slider(5, 0, 50)));

                Menu.AddSubMenu(drawingMenu);

                Menu.AddItem(new MenuItem(Name + "SSTimer", Language.Get("LastPosition_SSTimer")).SetValue(false));
                Menu.AddItem(new MenuItem(Name + "Enabled", Language.Get("G_Enabled")).SetValue(false));

                Menu.Item(Name + "DrawingTimeFormat").ValueChanged +=
                    (o, args) => _lastPositionObjects.ForEach(enemy => enemy.TextTotalSeconds = args.GetNewValue<StringList>().SelectedIndex == 1);
                Menu.Item(Name + "DrawingSSTimerOffset").ValueChanged +=
                    (o, args) => _lastPositionObjects.ForEach(enemy => enemy.FontOffset = args.GetNewValue<Slider>().Value);
                Menu.Item(Name + "SSTimer").ValueChanged +=
                    (o, args) => _lastPositionObjects.ForEach(enemy => enemy.SSTimer = args.GetNewValue<bool>());

                _parent.Menu.AddSubMenu(Menu);

                var teleport = false;

                if (Global.IoC.IsRegistered<Teleport>())
                {
                    var rt = Global.IoC.Resolve<Teleport>();

                    teleport = rt.Initialized && rt.Enabled;

                    rt.OnEnabled += TeleportEnabled;
                    rt.OnDisabled += TeleportDisabled;
                    rt.OnFinish += TeleportFinish;
                    rt.OnStart += TeleportStart;
                    rt.OnAbort += TeleportAbort;
                    rt.OnUnknown += TeleportAbort;
                }

                foreach (var enemy in HeroManager.Enemies)
                {
                    try
                    {
                        _lastPositionObjects.Add(new LastPositionObject(enemy, Menu.Item(Name + "DrawingFontSize").GetValue<Slider>().Value)
                        {
                            Active = Enabled,
                            TextTotalSeconds = Menu.Item(Name + "DrawingTimeFormat").GetValue<StringList>().SelectedIndex == 1,
                            FontOffset = Menu.Item(Name + "DrawingSSTimerOffset").GetValue<Slider>().Value,
                            SSTimer = Menu.Item(Name + "SSTimer").GetValue<bool>(),
                            Teleport = teleport
                        });
                    }
                    catch (Exception ex)
                    {
                        Global.Logger.AddItem(new LogItem(ex));
                    }
                }

                HandleEvents(_parent);
                RaiseOnInitialized();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        internal class LastPositionObject
        {
            private readonly Render.Sprite _championSprite;
            private readonly Render.Sprite _teleportSprite;
            private readonly Render.Text _text;
            public readonly Obj_AI_Hero Hero;
            private bool _active;
            private bool _added;
            public bool IsTeleporting;
            // ReSharper disable once InconsistentNaming
            public bool SSTimer;
            public int FontOffset = 5;
            public bool TextTotalSeconds;
            public bool Teleport;
            public bool Teleported;
            private float _lastSeen;

            public LastPositionObject(Obj_AI_Hero hero, int fontSize)
            {
                try
                {
                    Hero = hero;
                    var mPos = Drawing.WorldToMinimap(hero.Position);
                    var spawnPoint = ObjectManager.Get<GameObject>().FirstOrDefault(s => s is Obj_SpawnPoint && s.IsEnemy);

                    _championSprite =
                        new Render.Sprite(
                            (Bitmap) Resources.ResourceManager.GetObject(string.Format("LP_{0}", hero.ChampionName)) ?? Resources.LP_Aatrox,
                            new Vector2(mPos.X, mPos.Y))
                        {
                            VisibleCondition = delegate
                            {
                                try
                                {
                                    if (hero.IsVisible)
                                    {
                                        Teleported = false;
                                    }
                                    return Active && !Hero.IsVisible && !Hero.IsDead;
                                }
                                catch (Exception ex)
                                {
                                    Global.Logger.AddItem(new LogItem(ex));
                                    return false;
                                }
                            },
                            PositionUpdate = delegate
                            {
                                try
                                {
                                    if (Teleport && Teleported)
                                    {
                                        if (spawnPoint != null)
                                        {
                                            var p = Drawing.WorldToMinimap(spawnPoint.Position);
                                            return new Vector2(p.X - (_championSprite.Size.X/2), p.Y - (_championSprite.Size.Y/2));
                                        }
                                    }
                                    var pos = Drawing.WorldToMinimap(hero.Position);
                                    return new Vector2(pos.X - (_championSprite.Size.X/2), pos.Y - (_championSprite.Size.Y/2));
                                }
                                catch (Exception ex)
                                {
                                    Global.Logger.AddItem(new LogItem(ex));
                                    return default(Vector2);
                                }
                            }
                        };
                    _text = new Render.Text(string.Empty, new Vector2(mPos.X, mPos.Y), fontSize, Color.White)
                    {
                        OutLined = true,
                        Centered = true,
                        PositionUpdate = delegate
                        {
                            try
                            {
                                return new Vector2(_championSprite.Position.X + (_championSprite.Size.X/2),
                                    _championSprite.Position.Y + (_championSprite.Size.Y) + FontOffset);
                            }
                            catch (Exception ex)
                            {
                                Global.Logger.AddItem(new LogItem(ex));
                                return default(Vector2);
                            }
                        },
                        VisibleCondition = delegate
                        {
                            try
                            {
                                if (Hero.IsVisible && !Hero.IsDead)
                                    _lastSeen = Game.Time;
                                return SSTimer && _championSprite.Visible && _lastSeen != 0f && (Game.Time - _lastSeen) > 3f;
                            }
                            catch (Exception ex)
                            {
                                Global.Logger.AddItem(new LogItem(ex));
                                return false;
                            }
                        },
                        TextUpdate = () => _text.Visible ? (Game.Time - _lastSeen).FormatTime(TextTotalSeconds) : string.Empty
                    };
                    _teleportSprite = new Render.Sprite(Resources.LP_Teleport, new Vector2(mPos.X, mPos.Y))
                    {
                        VisibleCondition = delegate
                        {
                            try
                            {
                                return _championSprite.Visible && Teleport && IsTeleporting;
                            }
                            catch (Exception ex)
                            {
                                Global.Logger.AddItem(new LogItem(ex));
                                return false;
                            }
                        },
                        PositionUpdate = delegate
                        {
                            try
                            {
                                var pos = Drawing.WorldToMinimap(hero.Position);
                                return new Vector2(pos.X - (_teleportSprite.Size.X/2), pos.Y - (_teleportSprite.Size.Y/2));
                            }
                            catch (Exception ex)
                            {
                                Global.Logger.AddItem(new LogItem(ex));
                                return default(Vector2);
                            }
                        }
                    };
                }
                catch (Exception ex)
                {
                    Global.Logger.AddItem(new LogItem(ex));
                }
            }

            public bool Active
            {
                private get { return _active; }
                set
                {
                    _active = value;
                    Toggle();
                }
            }

            private void Toggle()
            {
                if (_championSprite == null)
                    return;

                if (_active && !_added)
                {
                    _teleportSprite.Add(0);
                    _championSprite.Add(1);
                    _text.Add(2);
                    _added = true;
                }
                else if (!_active && _added)
                {
                    _teleportSprite.Remove();
                    _championSprite.Remove();
                    _text.Remove();
                    _added = false;
                }
            }
        }
    }
}