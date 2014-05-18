using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;

//by honda (75%) & screeder (25%)

namespace Prediction
{
    public enum SkillshotType
    {
        SkillshotLine,
        SkillshotCircle,
    }

    public enum SkillshotAOEType
    {
        SkillshotLine,
        SkillshotCircle,
        SkillshotCone,
        //SkillshotArc,
    }

    public class PredictionOutput
    {
        public Vector3 CastPosition;
        public List<Obj_AI_Base> CollisionUnitsList;
        public int HitChance;
        public Vector3 Position;

        public int TargetsHit;

        public PredictionOutput(Vector2 cp, Vector2 pos, int HitChance)
        {
            CastPosition = Geometry.To3D(cp);
            Position = Geometry.To3D(pos);
            this.HitChance = HitChance;
            CollisionUnitsList = new List<Obj_AI_Base>();
            TargetsHit = 1;
        }
    }

    public class Dash
    {
        public Vector2 EndPos;
        public bool IsBlink;
        public float Speed;
        public float endT;
        public bool processed;

        public Dash(float endT, float Speed, bool IsBlink, Vector2 EndPos)
        {
            this.endT = endT;
            this.Speed = Speed;
            this.IsBlink = IsBlink;
            this.EndPos = EndPos;

            processed = true;
        }
    }

    public class BlinkData
    {
        public bool MC;
        public float delay;
        public float range;

        public BlinkData(float range, float delay, bool MC)
        {
            this.range = range;
            this.delay = delay;
            this.MC = MC;
        }
    }

    public class PredictionInternalOutput
    {
        public Vector3 CastPosition;
        public Vector3 Position;
        public bool Valid;

        public PredictionInternalOutput(Vector2 cp, Vector2 pos, bool Valid)
        {
            CastPosition = Geometry.To3D(cp);
            Position = Geometry.To3D(pos);
            this.Valid = Valid;
        }
    }

    public class VSPrediction
    {
        public class HitChance
        {
            public const int VP_Dashing = 4;
            public const int VP_Immobile = 3;
            public const int VP_HighHitchance = 2;
            public const int VP_LowHitchance = 1;
            public const int VP_CantHit = 0;
            public const int VP_Collision = -1;
        }

        public static Dictionary<int, Dash> Dashes = new Dictionary<int, Dash>();
        public static Dictionary<int, float> ImmobileT = new Dictionary<int, float>();

        public static Dictionary<string, BlinkData> Blinks = new Dictionary<string, BlinkData>();
        public static Dictionary<string, float> ImmobileData = new Dictionary<string, float>();
        public static Dictionary<string, float> DashData = new Dictionary<string, float>();

        static VSPrediction()
        {
            Obj_AI_Base.OnNewPath += OnNewPath;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpell;
            Game.OnGameProcessPacket += OnProcessPacket;
            Game.OnGameUpdate += OnTick;
            Game.OnWndProc += onwndmsg;
            //Drawing.OnDraw += Draw;

            Blinks.Add("summonerflash", new BlinkData(400, 0.6f, false));
            Blinks.Add("ezrealarcaneshift", new BlinkData(475, 0.8f, false));
            Blinks.Add("deceive", new BlinkData(400, 0.8f, false));
            Blinks.Add("riftwalk", new BlinkData(700, 0.8f, false));
            Blinks.Add("katarinae", new BlinkData(float.MaxValue, 0.8f, false));
            Blinks.Add("elisespideredescent", new BlinkData(float.MaxValue, 0.8f, false));
            Blinks.Add("elisespidere", new BlinkData(float.MaxValue, 0.8f, false));


            ImmobileData.Add("katarinar", 1.0f); //Katarina's R
            ImmobileData.Add("drain", 1.0f); //Fiddlesticks W
            ImmobileData.Add("consume", 0.5f); //Nunu's Q
            ImmobileData.Add("absolutezero", 1.0f); //Nunu's R
            ImmobileData.Add("rocketgrab", 0.5f); //Blitzcrank's Q
            ImmobileData.Add("staticfield", 0.5f); //Blitzcrank's R
            ImmobileData.Add("cassiopeiapetrifyinggaze", 0.5f); //Hackssiopeia's R
            ImmobileData.Add("ezrealtrueshotbarrage", 1.0f); //Ezreal's R
            ImmobileData.Add("galioidolofdurand", 1.0f); //Galio's R
            ImmobileData.Add("luxmalicecannon", 1.0f); //Lux's R
            ImmobileData.Add("reapthewhirlwind", 1.0f); //Janna's R
            ImmobileData.Add("jinxw", 0.6f); //Jinx's W
            ImmobileData.Add("jinxr", 0.6f); //Jinx's R
            ImmobileData.Add("missfortunebullettime", 1.0f); //MissFortune's R
            ImmobileData.Add("shenstandunited", 1.0f); //Shen's R
            ImmobileData.Add("threshe", 0.5f); //Tresh's E
            ImmobileData.Add("threshrpenta", 0.7f); //Tresh's R
            ImmobileData.Add("infiniteduress", 1.0f); //WarWicks R
            ImmobileData.Add("meditate", 1.0f); //MasterYi's W

            DashData.Add("ahritumble", 0.25f); //ahri's r
            DashData.Add("akalishadowdance", 0.25f); //akali r
            DashData.Add("headbutt", 0.25f); //alistar w
            DashData.Add("caitlynentrapment", 0.25f); //caitlyn e
            DashData.Add("carpetbomb", 0.25f); //corki w
            DashData.Add("dianateleport", 0.25f); //diana r
            DashData.Add("fizzpiercingstrike", 0.25f); //fizz q
            DashData.Add("fizzjump", 0.25f); //fizz e
            DashData.Add("gragasbodyslam", 0.25f); //gragas e
            DashData.Add("gravesmove", 0.25f); //graves e
            DashData.Add("ireliagatotsu", 0.25f); //irelia q
            DashData.Add("jarvanivdragonstrike", 0.25f); //jarvan q
            DashData.Add("jaxleapstrike", 0.25f); //jax q
            DashData.Add("khazixe", 0.25f); //khazix e and e evolved
            DashData.Add("leblancslide", 0.25f); //leblanc w
            DashData.Add("leblancslidem", 0.25f); //leblanc w (r)
            DashData.Add("blindmonkqtwo", 0.25f); //lee sin q
            DashData.Add("blindmonkwone", 0.25f); //lee sin w
            DashData.Add("luciane", 0.25f); //lucian e
            DashData.Add("maokaiunstablegrowth", 0.25f); //maokai w
            DashData.Add("pounce", 0.25f); //nidalees w
            DashData.Add("nocturneparanoia2", 0.25f); //nocturne r
            DashData.Add("pantheon_leapbash", 0.25f); //pantheon e?
            DashData.Add("renektonsliceanddice", 0.25f); //renekton e                 
            DashData.Add("riventricleave", 0.25f); //riven q          
            DashData.Add("rivenfeint", 0.25f); //riven e      
            DashData.Add("sejuaniarcticassault", 0.25f); //sejuani q
            DashData.Add("shenshadowdash", 0.25f); //shen e
            DashData.Add("shyvanatransformcast", 0.25f); //shyvana r
            DashData.Add("rocketjump", 0.25f); //tristana w
            DashData.Add("slashcast", 0.25f); //tryndamere e
            DashData.Add("vaynetumble", 0.25f); //vayne q
            DashData.Add("viq", 0.25f); //vi q
            DashData.Add("monkeykingnimbus", 0.25f); //wukong q
            DashData.Add("xenzhaosweep", 0.25f); //xin xhao q
            DashData.Add("yasuodashwrapper", 0.25f); //yasuo e
        }

        private static void onwndmsg(WndEventArgs args)
        {
            if (args.Msg == 0x100)
            {
                if (args.WParam == 74)
                {
                    // TList.Add(Utils.To2D(Game.CursorPos));
                }
            }
        }

        private static void OnTick(EventArgs args)
        {
            foreach (Obj_AI_Hero unit in ObjectManager.Get<Obj_AI_Hero>())
                if (Dashes.ContainsKey(unit.NetworkId))
                {
                    if (!Dashes[unit.NetworkId].processed)
                    {
                        float duration = Utils.GetPathLength(Utils.GetWaypoints(unit))/Dashes[unit.NetworkId].Speed;
                        Dashes[unit.NetworkId].endT = Game.Time + duration;
                        Dashes[unit.NetworkId].processed = true;
                        //Game.PrintChat("New Dash" + duration);
                    }
                }
        }

        private static void OnProcessSpell(Obj_AI_Base unit, GameObjectProcessSpellCastEventArgs args)
        {
            if (unit.Type == ObjectManager.Player.Type)
            {
                if (ImmobileData.ContainsKey(args.SData.Name.ToLower()))
                {
                    if (!ImmobileT.ContainsKey(unit.NetworkId))
                    {
                        ImmobileT.Add(unit.NetworkId, 0f);
                    }
                    ImmobileT[unit.NetworkId] = Game.Time + ImmobileData[args.SData.Name.ToLower()];
                }

                if (Blinks.ContainsKey(args.SData.Name.ToLower()))
                {
                    BlinkData bdata = Blinks[args.SData.Name.ToLower()];
                    Vector2 endPos = Geometry.To2D(args.End);
                    if (Vector2.DistanceSquared(endPos, Geometry.To2D(unit.ServerPosition)) > bdata.range * bdata.range)
                    {
                        Vector2 Direction = endPos - Geometry.To2D(unit.ServerPosition);
                        Direction.Normalize();
                        endPos = Geometry.To2D(unit.ServerPosition) + Direction * bdata.range;
                    }

                    Vector3[] p = unit.GetPath(Geometry.To3D(endPos));
                    endPos = Geometry.To2D(p[p.Count() - 1]);

                    if (!Dashes.ContainsKey(unit.NetworkId))
                        Dashes.Add(unit.NetworkId, new Dash(0, 0, true, new Vector2()));

                    Dashes[unit.NetworkId].endT = Game.Time + bdata.delay;
                    Dashes[unit.NetworkId].EndPos = endPos;
                    Dashes[unit.NetworkId].IsBlink = true;
                }

                if (DashData.ContainsKey(args.SData.Name.ToLower()))
                {
                    //TODO: Needs to get calculated
                }
            }
        }

        private static void OnProcessPacket(GamePacketProcessEventArgs args)
        {
            /*Dash*/
            if (args.PacketId == 99)
            {
                var stream = new MemoryStream(args.PacketData);
                var b = new BinaryReader(stream);
                b.BaseStream.Position = b.BaseStream.Position + 12;
                int NetworkID = BitConverter.ToInt32(b.ReadBytes(4), 0);
                float Speed = BitConverter.ToSingle(b.ReadBytes(4), 0);
                var unit = ObjectManager.GetUnitByNetworkId<Obj_AI_Base>(NetworkID);
                if (unit.IsValid && unit.Type == GameObjectType.obj_AI_Hero)
                {
                    if (!Dashes.ContainsKey(unit.NetworkId))
                        Dashes.Add(unit.NetworkId, new Dash(0, 0, false, new Vector2()));

                    Dashes[unit.NetworkId].processed = false;
                    Dashes[unit.NetworkId].Speed = Speed;
                    Dashes[unit.NetworkId].IsBlink = false;
                }
            }
        }

        private static void OnNewPath(Obj_AI_Base unit, EventArgs args)
        {
            //if (unit.Type == ObjectManager.Player.Type)
            //    Game.PrintChat(Game.Time + " New path for: " + unit.BaseSkinName + " Length: " +
            //                   Utils.GetPathLength(Utils.GetWaypoints(unit)));
        }

        private static void Draw(EventArgs args)
        {
            foreach (Obj_AI_Hero hero in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (hero.Team != ObjectManager.Player.Team)
                {
                    PredictionOutput Out = GetBestPosition(hero, 0.25f, 75, 1000, Game.CursorPos, 900, true, SkillshotType.SkillshotCircle, Game.CursorPos);

                    if (Out == null)
                        continue;

                    Drawing.DrawCircle(Out.CastPosition, 200, Color.White);
                    Drawing.DrawCircle(Out.Position, 200, Color.GreenYellow);
                    Drawing.DrawText(100, 100, Color.White, Out.HitChance.ToString());

                    foreach (Obj_AI_Base units in Out.CollisionUnitsList)
                    {
                        Drawing.DrawCircle(units.ServerPosition, 100, Color.Red);
                    }
                }
            }
        }

        private static float GetHitBox(Obj_AI_Base unit)
        {
            return unit.BoundingRadius;
        }

        private static float ExtraDelay()
        {
            return 0.07f + Game.Ping/1000;
        }

        public static float ImmobileTime(Obj_AI_Base unit)
        {
            float result = ImmobileT.ContainsKey(unit.NetworkId) ? ImmobileT[unit.NetworkId] : 0f;

            foreach (BuffInstance buff in unit.Buffs)
            {
                if (buff.IsActive && Game.Time <= buff.EndTime &&
                    (buff.Type == BuffType.Charm ||
                     buff.Type == BuffType.Knockup ||
                     buff.Type == BuffType.Stun ||
                     buff.Type == BuffType.Suppression ||
                     buff.Type == BuffType.Snare))
                {
                    result = Math.Max(result, buff.EndTime);
                }
            }
            return result;
        }

        public static PredictionOutput GetBestPosition(Obj_AI_Base unit, float delay, float width, float speed, Vector3 from, 
            float range, bool collision, SkillshotType stype, Vector3 rangeCheckFrom = new Vector3())
        {
            var result = new PredictionOutput(new Vector2(), new Vector2(), 0);
            if (rangeCheckFrom.X.CompareTo(0) == 0)
            {
                rangeCheckFrom = ObjectManager.Player.ServerPosition;
            }
            if (!Utility.ValidTarget(unit))
            {
                return result;
            }
            delay += ExtraDelay();
            width = Math.Max(width - 5, 1) + GetHitBox(unit);

            float ImmobileT = ImmobileTime(unit);
            if (Dashes.ContainsKey(unit.NetworkId) && Dashes[unit.NetworkId].endT >= Game.Time)
            {
                /*Unit Dashing*/

                if (!Dashes[unit.NetworkId].IsBlink)
                {
                    PredictionInternalOutput iPrediction = GetUnitPosition(Utils.GetWaypoints(unit),
                        Dashes[unit.NetworkId].Speed, delay, speed, 1, Geometry.To2D(from));

                    if (iPrediction.Valid)
                    {
                        /* Mid air */
                        result.CastPosition = iPrediction.CastPosition;
                        result.Position = iPrediction.Position;
                        result.HitChance = HitChance.VP_Dashing;
                    }
                    else
                    {
                        /* Check if we can hit after landing */
                        Vector2 endPoint = Utils.GetWaypoints(unit)[Utils.GetWaypoints(unit).Count - 1];
                        float landtime = Game.Time +
                                         Vector2.Distance(endPoint,
                                             Geometry.To2D(from)) / speed + delay;

                        if ((landtime - Dashes[unit.NetworkId].endT)*unit.MoveSpeed < width*1.25 ||
                            unit.BaseSkinName == "Riven")
                        {
                            result.CastPosition = Geometry.To3D(endPoint);
                            result.Position = Geometry.To3D(endPoint);
                            result.HitChance = HitChance.VP_Dashing;
                        }
                        else
                        {
                            result.CastPosition = Geometry.To3D(endPoint);
                            result.Position = Geometry.To3D(endPoint);
                            result.HitChance = HitChance.VP_CantHit;
                        }
                    }
                }
                    /*Blinks*/
                else
                {
                    Vector2 endPoint = Dashes[unit.NetworkId].EndPos;
                    float totaldelay = delay + Vector2.Distance(endPoint, Geometry.To2D(from)) / speed;

                    result.Position = Geometry.To3D(endPoint);
                    result.CastPosition = Geometry.To3D(endPoint);

                    if ((Dashes[unit.NetworkId].endT - Game.Time + width/unit.MoveSpeed) >= totaldelay)
                    {
                        result.HitChance = HitChance.VP_Dashing;
                    }
                    else //TODO:Get the location where he is most likely going after blinking
                    {
                        result.HitChance = HitChance.VP_CantHit;
                    }
                }
            }
            else if (ImmobileT >= Game.Time)
            {
                float timeToHit = delay - width/unit.MoveSpeed +
                                  Vector2.Distance(Geometry.To2D(unit.ServerPosition), Geometry.To2D(from)) / speed;

                if (ImmobileT - Game.Time >= timeToHit)
                {
                    /* Unit will  be immobile */
                    result.CastPosition = unit.ServerPosition;
                    result.Position = unit.ServerPosition;
                    result.HitChance = HitChance.VP_Immobile;
                }
                else
                {
                    /* Unit will be able to escape if we cast just in the position he is. TODO: Calculate the escape route he will likely take */
                    result.CastPosition = unit.ServerPosition;
                    result.Position = unit.ServerPosition;
                    result.HitChance = HitChance.VP_CantHit;
                }
            }
            else
            {
                List<Vector2> Waypoints = Utils.GetWaypoints(unit);
                if (Waypoints.Count == 1)
                {
                    /*Unit not moving*/
                    result.CastPosition = unit.ServerPosition;
                    result.Position = unit.ServerPosition;
                    result.HitChance = HitChance.VP_HighHitchance;
                }
                else
                {
                    PredictionInternalOutput iPrediction = GetUnitPosition(Waypoints, unit.MoveSpeed, delay, speed,
                        width, Geometry.To2D(from));

                    if (iPrediction.Valid)
                    {
                        result.CastPosition = iPrediction.CastPosition;
                        result.Position = iPrediction.Position;
                        result.HitChance = HitChance.VP_HighHitchance;
                            /* Change the hitchance according to the path change rate */
                    }
                    else
                    {
                        result.CastPosition = iPrediction.CastPosition;
                        result.Position = iPrediction.Position;
                        result.HitChance = HitChance.VP_CantHit;
                    }
                }
            }

            if (range != float.MaxValue)
            {
                if (stype == SkillshotType.SkillshotLine)
                {
                    if (Vector2.DistanceSquared(Geometry.To2D(rangeCheckFrom), Geometry.To2D(result.Position)) >= range * range)
                    {
                        result.HitChance = HitChance.VP_CantHit;
                    }

                    if (Vector2.DistanceSquared(Geometry.To2D(rangeCheckFrom), Geometry.To2D(result.CastPosition)) >=
                        range*range)
                    {
                        result.HitChance = HitChance.VP_CantHit;
                    }
                }
                else
                {
                    if (Vector2.DistanceSquared(Geometry.To2D(rangeCheckFrom), Geometry.To2D(result.Position)) >=
                        Math.Pow(range + width, 2))
                    {
                        result.HitChance = HitChance.VP_CantHit;
                    }
                    if (Vector2.DistanceSquared(Geometry.To2D(rangeCheckFrom), Geometry.To2D(result.CastPosition)) >=
                        Math.Pow(range + width, 2))
                    {
                        result.HitChance = HitChance.VP_CantHit;
                    }
                }
            }

            if (collision && result.HitChance > HitChance.VP_CantHit)
            {
                var CheckLocations = new List<Vector2>();
                CheckLocations.Add(Geometry.To2D(result.Position));
                CheckLocations.Add(Geometry.To2D(unit.ServerPosition));
                CheckLocations.Add(Geometry.To2D(result.CastPosition));

                List<Obj_AI_Base> Col1 = GetCollision(Geometry.To2D(from), CheckLocations, stype, width - GetHitBox(unit),
                    delay, speed, range);

                if (Col1.Count > 0)
                {
                    result.HitChance = HitChance.VP_Collision;
                    result.CollisionUnitsList.AddRange(Col1);
                }
            }

            return result;
        }

        public static PredictionOutput GetBestAOEPosition(Obj_AI_Base unit, float delay, float width, float speed,
                                                           Vector3 from, float range, bool collision,
                                                           SkillshotAOEType spelltype, Vector3 rangeCheckFrom = new Vector3(), float accel = -1483)
        {
            PredictionOutput objects = new PredictionOutput(new Vector2(), new Vector2(), 0);
            if (rangeCheckFrom.X.CompareTo(0) == 0)
            {
                rangeCheckFrom = ObjectManager.Player.ServerPosition;
            }
            switch (spelltype)
            {
                case SkillshotAOEType.SkillshotLine:
                    objects = GetAoeLinePrediction(unit, width, range, delay, speed, collision, from, rangeCheckFrom);
                    break;
                case SkillshotAOEType.SkillshotCircle:
                    objects = GetAoeCirclePrediction(unit, width, range, delay, speed, collision, from, rangeCheckFrom);
                    break;
                case SkillshotAOEType.SkillshotCone:
                    objects = GetAoeConePrediction(unit, width, range, delay, speed, collision, from, rangeCheckFrom);
                    break;
                //case SkillshotAOEType.SkillshotArc:
                //    objects = GetAoeArcPrediction(unit, width, range, delay, speed, collision, from, rangeCheckFrom, accel);
                //    break;
            }

            return objects;
        }

        public static List<Obj_AI_Base> GetCollision(Vector2 from, List<Vector2> To, SkillshotType stype, float width,
            float delay, float speed, float range)
        {
            var result = new List<Obj_AI_Base>();
            delay -= ExtraDelay();

            foreach (Vector2 TestPosition in To)
            {
                foreach (Obj_AI_Minion collisionObject in ObjectManager.Get<Obj_AI_Minion>())
                {
                    if (collisionObject.Team != ObjectManager.Player.Team && Utility.ValidTarget(collisionObject) &&
                        Vector2.DistanceSquared(from, Geometry.To2D(collisionObject.Position)) <= Math.Pow(range * 1.5, 2))
                    {
                        PredictionOutput objectPrediction = GetBestPosition(collisionObject, delay, width, speed, Geometry.To3D(from), float.MaxValue,
                            false, stype, Geometry.To3D(from));
                        if (Geometry.GetDistanceToLineSegment(from, TestPosition, Geometry.To2D(objectPrediction.Position),true,true) <=
                            Math.Pow((width + 15 + collisionObject.BoundingRadius), 2))
                        {
                            result.Add(collisionObject);
                            Drawing.DrawCircle(objectPrediction.Position, width + collisionObject.BoundingRadius,
                                Color.Red);
                        }
                    }
                }
            }

            /*Remove the duplicates*/
            result = result.Distinct().ToList();
            return result;
        }

        private static PredictionInternalOutput GetUnitPosition(List<Vector2> waypoints, float unitSpeed, float delay,
            float missileSpeed, float width, Vector2 from)
        {
            var result = new PredictionInternalOutput(new Vector2(), new Vector2(), false);

            if (Utils.GetPathLength(waypoints) > (delay*unitSpeed - width))
            {
                List<Vector2> path = Utils.CutPath(waypoints, delay*unitSpeed, width);

                if (missileSpeed == float.MaxValue)
                {
                    /*Spell with only a delay*/
                    Vector2 Direction = path[1] - path[0];
                    Direction.Normalize();
                    result.Position = Geometry.To3D(path[0]);
                    result.CastPosition = Geometry.To3D(path[0] - Direction * width);

                    if ((path.Count == 2) && (Vector2.DistanceSquared(path[0], path[1]) <= width*width))
                    {
                        result.Position = Geometry.To3D(path[1] - Direction * width);
                    }

                    result.Valid = true;
                    return result;
                }

                /*Spell with delay and missile*/
                float T = 0f;
                float Tb = 0f;
                int k = 0;
                for (int i = 0; i < path.Count - 1; i++)
                {
                    Vector2 A = path[i];
                    Vector2 B = path[i + 1];


                    object[] Sol = Geometry.VectorMovementCollision(A, B, unitSpeed, from, missileSpeed);
                    var t1 = (float) Sol[0];
                    var p1 = (Vector2) Sol[1];
                    float t = float.NaN;
                    float Tc = Tb;
                    Tb = T + Vector2.Distance(A, B)/unitSpeed;

                    if (!float.IsNaN(t1))
                    {
                        if (((t1 >= T) && (t1 <= Tb))
                            )
                        {
                            t = t1;
                        }
                        else if (t1 > Tb && i != path.Count - 2)
                        {
                            t = t1 - (Tb - T);
                            Vector2 nDirection = path[i + 2] - B;
                            nDirection.Normalize();
                            p1 = B + t*unitSpeed*nDirection;
                        }
                    }

                    if (!float.IsNaN(t))
                    {
                        Vector2 Direction = B - A;
                        Direction.Normalize();
                        Vector2 hitPosition = p1;
                        result.CastPosition = Geometry.To3D(hitPosition - width * Direction);
                        result.Valid = true;

                        if ((i == path.Count - 2) && (Vector2.DistanceSquared(hitPosition, B) <= width*width))
                        {
                            hitPosition = B - width*Direction;
                        }

                        result.Position = Geometry.To3D(hitPosition);
                        return result;
                    }

                    T = Tb;
                }

                /*No solution found*/
                result.CastPosition = Geometry.To3D(waypoints[waypoints.Count - 1]);
                result.Position = Geometry.To3D(waypoints[waypoints.Count - 1]);
                result.Valid = false;
                return result;
            }
            /*Not enough waypoints. (Path too short)*/
            result.CastPosition = Geometry.To3D(waypoints[waypoints.Count - 1]);
            result.Position = Geometry.To3D(waypoints[waypoints.Count - 1]);
            result.Valid = false;
            return result;
        }

        private static PredictionOutput GetAoeCirclePrediction(Obj_AI_Base unit, float width, float range,
            float delay, float speed, bool collision, Vector3 from, Vector3 rangeCheckFrom)
        {
            PredictionOutput result = GetBestPosition(unit, delay, width, speed, from, range, collision, SkillshotType.SkillshotCircle, rangeCheckFrom);
            var Points = new List<Vector2>();

            Points.Add(Geometry.To2D(result.Position));

            foreach (Obj_AI_Hero enemy in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (enemy.Team != ObjectManager.Player.Team && Utility.ValidTarget(enemy) &&
                    enemy.NetworkId != unit.NetworkId && Vector3.Distance(from, enemy.ServerPosition) <= range*1.2)
                {
                    PredictionOutput pred = GetBestPosition(enemy, delay, width, speed, from, range, collision, SkillshotType.SkillshotCircle, rangeCheckFrom);

                    if (pred.HitChance >= HitChance.VP_CantHit)
                    {
                        Points.Add(Geometry.To2D(pred.Position));
                    }
                }
            }

            while (Points.Count > 1)
            {
                Vector2 center;
                float radius;
                MEC.GetMec(Points, out center, out radius);

                if (radius <= width * 0.8 + GetHitBox(unit) - 8 &&
                    Vector2.DistanceSquared(center, Geometry.To2D(rangeCheckFrom)) < range * range)
                {
                    result.CastPosition = Geometry.To3D(center);
                    result.TargetsHit = Points.Count;
                    return result;
                }

                float maxdist = -1;
                int maxdistindex = 1;

                for (int i = 1; i < Points.Count; i++)
                {
                    float distance = Vector2.DistanceSquared(Points[i], Points[0]);
                    if (distance > maxdist || maxdist.CompareTo(-1) == 0)
                    {
                        maxdistindex = i;
                        maxdist = distance;
                    }
                }

                Points.RemoveAt(maxdistindex);
            }

            return result;
        }

        private static Vector2[] GetPossiblePoints(Vector2 from, Vector2 pos, float width, float range)
        {
            Vector2 middlePoint = (from + pos) / 2;
            Vector2[] vectors = Geometry.CircleCircleIntersection(from, middlePoint, width,
                                                               Vector2.Distance(middlePoint, from));
            Vector2 P1 = vectors[0];
            Vector2 P2 = vectors[1];

            Vector2 V1 = (P1 - from);
            Vector2 V2 = (P2 - from);

            V1 = (pos - V1 - from);
            V1.Normalize();
            V1 = Vector2.Multiply(V1, range);
            V1 = V1 + from;
            V2 = (pos - V2 - from);
            V2.Normalize();
            V2 = Vector2.Multiply(V2, range);
            V2 = V2 + from;
            return new Vector2[] { V1, V2 };
        }

        private static Object[] CountHits(Vector2 P1, Vector2 P2, float width, List<Vector2> points)
        {
            int hits = 0;
            List<Vector2> nPoints = new List<Vector2>();
            width = width + 2;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 point = points[i];
                Object[] objects = Geometry.VectorPointProjectionOnLineSegment(P1, P2, point);
                Vector2 pointSegment = (Vector2)objects[0];
                bool isOnSegment = (bool)objects[2];
                if (isOnSegment && Vector2.DistanceSquared(pointSegment, point) <= width * width)
                {
                    hits = hits + 1;
                    nPoints.Add(point);
                }
                else if (i == 0)
                {
                    return new Object[] { hits, nPoints };
                }
            }
            return new Object[] { hits, nPoints };
        }

        private static PredictionOutput GetAoeLinePrediction(Obj_AI_Base unit, float width, float range, float delay, float speed,
                                                                bool collision, Vector3 from, Vector3 rangeCheckFrom)
        {
            PredictionOutput result = GetBestPosition(unit, delay, width, speed, from, range, collision, SkillshotType.SkillshotLine, rangeCheckFrom);
            var points = new List<Vector2>();

            points.Add(Geometry.To2D(result.Position));

            foreach (Obj_AI_Hero enemy in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (enemy.IsEnemy && enemy.NetworkId != unit.NetworkId && Utility.ValidTarget(enemy) && !enemy.IsDead && enemy.IsValid &&
                    Vector3.DistanceSquared(enemy.ServerPosition, ObjectManager.Player.ServerPosition) <=
                    (range * 1.2) * (range * 1.2))
                {
                    PredictionOutput pred = GetBestPosition(enemy, delay, width, speed, from, range, collision, SkillshotType.SkillshotLine, rangeCheckFrom);

                    if (pred.HitChance >= HitChance.VP_CantHit)
                    {
                        points.Add(Geometry.To2D(pred.Position));
                    }
                }
            }

            int maxHit = 1;
            Vector2 maxHitPos = new Vector2();
            List<Vector2> maxHitPoints = new List<Vector2>();

            if (points.Count > 1)
            {
                width += unit.BoundingRadius * 3 / 4;
                for (int i = 0; i < points.Count; i++)
                {
                    Vector2[] possiblePoints = GetPossiblePoints(Geometry.To2D(from), points[i], width - 20, range);
                    Vector2 C1 = possiblePoints[0], C2 = possiblePoints[1];
                    Object[] countHits1 = CountHits(Geometry.To2D(from), C1, width, points);
                    Object[] countHits2 = CountHits(Geometry.To2D(from), C2, width, points);
                    if ((int)countHits1[0] >= maxHit)
                    {
                        maxHitPos = C1;
                        maxHit = (int)countHits1[0];
                        maxHitPoints = (List<Vector2>)countHits1[1];
                    }
                    if ((int)countHits2[0] >= maxHit)
                    {
                        maxHitPos = C2;
                        maxHit = (int)countHits2[0];
                        maxHitPoints = (List<Vector2>)countHits2[1];
                    }
                }
            }

            if (maxHit > 1)
            {
                float maxDistance = -1;
                Vector2 p1 = new Vector2(), p2 = new Vector2();
                for (int i = 0; i < maxHitPoints.Count; i++)
                {
                    for (int j = 0; j < maxHitPoints.Count; j++)
                    {
                        Vector2 startP = Geometry.To2D(from);
                        Vector2 endP = (maxHitPoints[i] + maxHitPoints[j]) / 2;
                        Object[] objects01 = Geometry.VectorPointProjectionOnLineSegment(startP, endP, maxHitPoints[i]);
                        Vector2 pointSegment01 = (Vector2)objects01[0];
                        Vector2 pointLine01 = (Vector2)objects01[1];
                        bool isOnSegment01 = (bool)objects01[2];
                        Object[] objects02 = Geometry.VectorPointProjectionOnLineSegment(startP, endP, maxHitPoints[j]);
                        Vector2 pointSegment02 = (Vector2)objects02[0];
                        Vector2 pointLine02 = (Vector2)objects02[1];
                        bool isOnSegment02 = (bool)objects02[2];
                        float dist =
                            (float)
                            (Vector2.DistanceSquared(maxHitPoints[i], pointLine01) +
                             Vector2.DistanceSquared(maxHitPoints[j], pointLine02));
                        if (dist >= maxDistance)
                        {
                            maxDistance = dist;
                            result.CastPosition = Geometry.To3D((p1 + p2) / 2);
                            result.TargetsHit = maxHit;
                            p1 = maxHitPoints[i];
                            p2 = maxHitPoints[j];
                        }
                    }
                }
                return result;
            }
            return result;
        }

        private static Object[] CountVectorBetween(Vector2 V1, Vector2 V2, List<Vector2> points)
        {
            int result = 0;
            List<Vector2> hitpoints = new List<Vector2>();
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 t = points[i];
                Vector2 NVector = Geometry.Vector2CrossP(V1, t);
                Vector2 NVector2 = Geometry.Vector2CrossP(t, V2);
                if (NVector.Y >= 0 && NVector2.Y >= 0)
                {
                    result = result + 1;
                    hitpoints.Add(t);
                }
                else if (i == 0)
                {
                    return new object[] { -1, hitpoints };
                }
            }
            return new object[] { result, hitpoints };
        }

        private static Object[] CheckHit(Vector2 position, float angle, List<Vector2> points)
        {
            Vector2 v1 = Geometry.Vector2Rotate(position, -angle / 2);
            Vector2 v2 = Geometry.Vector2Rotate(position, angle / 2);
            return CountVectorBetween(v1, v2, points);
        }

        private static PredictionOutput GetAoeConePrediction(Obj_AI_Base unit, float angle, float range, float delay, float speed,
                                                                bool collision, Vector3 from, Vector3 rangeCheckFrom)
        {
            PredictionOutput result = GetBestPosition(unit, delay, 1, speed, from, range, collision, SkillshotType.SkillshotLine, rangeCheckFrom);
            var points = new List<Vector2>();

            points.Add(Geometry.To2D(result.Position));

            foreach (Obj_AI_Hero enemy in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (enemy.IsEnemy && enemy.NetworkId != unit.NetworkId && Utility.ValidTarget(enemy) && !enemy.IsDead && enemy.IsValid &&
                    Vector3.Distance(from, enemy.ServerPosition) <= range)
                {
                    PredictionOutput pred = GetBestPosition(enemy, delay, 1, speed, from, range, collision, SkillshotType.SkillshotLine, rangeCheckFrom);

                    if (pred.HitChance >= HitChance.VP_CantHit)
                    {
                        points.Add(Geometry.To2D(pred.Position));
                    }
                }
            }

            int maxHit = 1;
            Vector2 maxHitPos = new Vector2();
            List<Vector2> maxHitPoints = new List<Vector2>();

            if (points.Count > 1)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    Vector2 point = points[i];
                    Vector2 pos1 = Geometry.Vector2Rotate(point, angle / 2);
                    Vector2 pos2 = Geometry.Vector2Rotate(point, -angle / 2);

                    Object[] objects3 = CheckHit(pos1, angle, points);
                    int hits3 = (int)objects3[0];
                    List<Vector2> points3 = (List<Vector2>)objects3[1];
                    Object[] objects4 = CheckHit(pos2, angle, points);
                    int hits4 = (int)objects4[0];
                    List<Vector2> points4 = (List<Vector2>)objects4[1];

                    if (hits3 >= maxHit)
                    {
                        maxHitPos = pos1;
                        maxHit = hits3;
                        maxHitPoints = points3;
                    }
                    if (hits4 >= maxHit)
                    {
                        maxHitPos = pos2;
                        maxHit = hits4;
                        maxHitPoints = points4;
                    }
                }
            }

            if (maxHit > 1)
            {
                float maxangle = -1;
                Vector2 p1 = new Vector2();
                Vector2 p2 = new Vector2();
                for (int i = 0; i < maxHitPoints.Count; i++)
                {
                    Vector2 hitp = maxHitPoints[i];
                    for (int j = 0; j < maxHitPoints.Count; j++)
                    {
                        Vector2 hitp2 = maxHitPoints[j];
                        float cangle = Geometry.AngleBetween(new Vector2(), hitp2, hitp);
                        if (cangle > maxangle)
                        {
                            maxangle = cangle;
                            result.CastPosition = Geometry.To3D((((p1) + (p2)) / 2));
                            result.TargetsHit = maxHit;
                            p1 = hitp;
                            p2 = hitp2;
                        }
                    }
                }
                return result;
            }
            return result;
        }

        private static bool AreClockwise(Vector2 vec1, Vector2 vec2)
        {
            return ((-vec1.X * vec2.Y + vec1.Y * vec2.X) > 0);
        }

        private static float[] GetBoundingVectors(List<Vector2> targets)
        {
            int n = 1, largeN = 0;
	        Vector2 v1 = new Vector2(), v2 = new Vector2(), v3 = new Vector2();
	        Vector2 largeV1 = new Vector2(), largeV2 = new Vector2();
	        float theta1 = 0, theta2 = 0;

	        if( targets.Count >= 2 )
	        {
		        for( int i = 0; i < targets.Count; i++ )
		        {
			        for( int j = 0; j < targets.Count; j++ )
			        {
				        if( i != j )
				        {
				            v1 = new Vector2(targets[i].X - ObjectManager.Player.ServerPosition.X,
				                             targets[i].Y - ObjectManager.Player.ServerPosition.Y);
				            v2 = new Vector2(targets[j].X - ObjectManager.Player.ServerPosition.X,
				                             targets[j].Y - ObjectManager.Player.ServerPosition.Y);
					        if( targets.Count == 2 )
					        {
						        largeV1 = v1;
						        largeV2 = v2;
					        }
					        else
					        {
						        int tempN = 0;
						        for( int k = 0; k < targets.Count; k++ )
						        {
							        if( k != i && k != j )
							        {
                                        v3 = new Vector2(targets[k].X - ObjectManager.Player.ServerPosition.X, 
                                            targets[k].Y - ObjectManager.Player.ServerPosition.Y);
								        if( AreClockwise( v3, v1 ) && !AreClockwise( v3, v2 ) )
								        {
									        tempN = tempN + 1;
								        }
							        }
						        }
						        if( tempN > largeN )
						        {
							        largeN = tempN;
							        largeV1 = v1;
							        largeV2 = v2;
						        }
					        }
				        }
			        }
		        }
	        }
            theta1 = Geometry.Polar(largeV1) - 20;
            theta2 = Geometry.Polar(largeV2) + 20;
	        if( theta2 < theta1 )
	        {
		        theta1 = theta1 - 360;
	        }
	        return new float[] { theta1, theta2 };
        }

        private static Object[] CrescentCollision(List<Vector2> points, Vector2 from, float rangeMax, float accel)
        {
            float thetaIterator = 5; //increase to improve performance (0 - 10)
            float rangeIterator = 5; //increase to improve performance (from 0-100)
            float roundRange = 200; //higher means more minions collected, but possibly less accurate.

            Vector2 targetOriginal = new Vector2();
            List<Vector2> targetArray = points;
            Vector2  tsTargetOriginal = new Vector2();
            float theta, tsTargetAngle = 0.0f, targetAngle, tsAngle, tsVo, tsTestZ, angle, vo, testZ;
            Vector2 tsTarget, target = new Vector2();
            bool tsFlag = false;
            int highestCollision = 0;
            float highestAngle = 0;
            float highestRange = 0;
            if (points.Count > 0)
            {
                tsTargetOriginal = new Vector2(points[0].X - from.X, points[0].Y - from.Y);
                tsTargetAngle = Geometry.Polar(tsTargetOriginal);
            }
            if (points.Count > 1)
            {
                float[] thetas = GetBoundingVectors(targetArray);
                float rightTheta = thetas[0], leftTheta = thetas[1];
                for (float newTheta = rightTheta; newTheta < leftTheta; newTheta = newTheta + thetaIterator )
                {
                    theta = Geometry.DegreeToRadian(newTheta);
                    for (float range = 400; range < rangeMax; range = range + rangeIterator)
                    {
                        if (highestCollision < targetArray.Count)
                        {
                            int collisionCount = 0;
                            tsTargetOriginal = new Vector2(points[0].X - from.X, points[0].Y - from.Y);
                            tsTarget = Geometry.Vector2Rotate(tsTargetOriginal, theta);
                            tsAngle = Geometry.DegreeToRadian((-47) - (830 - range) / (-20)); //interpolate launch angle
                            tsVo = (float)Math.Sqrt((range*accel)/Math.Sin(2*tsAngle)); //initial velocity
                            tsTestZ = (float)(Math.Tan(tsAngle)*tsTarget.X -
                                      (accel/(Math.Pow(2*tsVo, 2)*Math.Pow(Math.Cos(tsAngle), 2)))*Math.Pow(tsTarget.X, 2));
                            if (Math.Abs(Math.Ceiling(tsTestZ) - Math.Ceiling(points[0].Y)) <= roundRange)
                            {
                                tsFlag = true;
                                collisionCount = collisionCount + 1;
                            }
                            else
                            {
                                tsFlag = false;
                            }

                            if (tsFlag)
                            {
                                foreach (Vector2 hero in targetArray)
                                {
                                    if (hero.X.CompareTo(targetArray[0].X) == 0)
                                    {
                                        continue;
                                    }
                                    targetOriginal = new Vector2(hero.X - from.X, hero.Y - from.Y);
                                    targetAngle = Geometry.Polar(targetOriginal);

                                    if ((targetAngle <= newTheta) &&
                                        ((tsTargetAngle <= newTheta)))
                                        //angle of theta must be greater than target
                                    {
                                        target = Geometry.Vector2Rotate(targetOriginal, theta); //rotate to neutral axis
                                        angle = Geometry.DegreeToRadian((-47) - (830 - range) / (-20));
                                            //interpolate launch angle
                                        vo = (float) Math.Sqrt((range*accel)/Math.Sin(2*angle)); //initial velocity
                                        testZ =
                                            (float)
                                            (Math.Tan(angle)*target.X -
                                             (accel/(Math.Pow(2*vo, 2)*Math.Pow(Math.Cos(angle), 2)))*
                                             Math.Pow(target.X, 2));

                                        if (Math.Abs(Math.Ceiling(testZ) - Math.Ceiling(target.Y)) <= roundRange)
                                            //compensate for rounding
                                            //collision detected
                                        {
                                            collisionCount = collisionCount + 1;
                                        }

                                        if (collisionCount > highestCollision)
                                        {
                                            highestCollision = collisionCount;
                                            highestAngle = theta; //in radians
                                            highestRange = range;
                                        }
                                    }
                                }

                            }
                        }
                    
                    }
                }
            }
            return new Object[] {(float)(ObjectManager.Player.ServerPosition.X + highestRange * Math.Cos(highestAngle)),
                                 (float)(ObjectManager.Player.ServerPosition.Y + highestRange * Math.Sin(highestAngle)),
                                 highestCollision};
        }

        //Not working like it should
        private static PredictionOutput GetAoeArcPrediction(Obj_AI_Base unit, float width, float range, float delay, float speed,
                                                                bool collision, Vector3 from, Vector3 rangeCheckFrom, float accel)
        {
            PredictionOutput result = GetBestPosition(unit, delay, width, speed, from, range, collision, SkillshotType.SkillshotLine, rangeCheckFrom);
            var points = new List<Vector2>();

            points.Add(Geometry.To2D(result.Position));

            foreach (Obj_AI_Hero enemy in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (enemy.IsEnemy && enemy.NetworkId != unit.NetworkId && Utility.ValidTarget(enemy) && !enemy.IsDead && enemy.IsValid &&
                    Vector3.Distance(from, enemy.ServerPosition) <= range)
                {
                    PredictionOutput pred = GetBestPosition(enemy, delay, width, speed, from, range, collision, SkillshotType.SkillshotLine, rangeCheckFrom);

                    if (pred.HitChance >= HitChance.VP_CantHit)
                    {
                        points.Add(Geometry.To2D(pred.Position));
                    }
                }
            }
            int maxHit = 1;
            Vector2 maxHitPos = new Vector2();
            List<Vector2> maxHitPoints = new List<Vector2>();

            if (points.Count > 1)
            {
                Object[] obj = CrescentCollision(points, Geometry.To2D(from), range, accel);
                result.CastPosition = Geometry.To3D(new Vector2((float)obj[0], (float)obj[1]));
                result.TargetsHit = (int)obj[2];
            }

            return result;
        }
    }
}