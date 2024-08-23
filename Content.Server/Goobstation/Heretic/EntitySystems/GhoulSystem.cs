using Content.Server.Administration.Systems;
using Content.Server.Antag;
using Content.Server.Atmos.Components;
using Content.Server.Body.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Humanoid;
using Content.Server.Mind.Commands;
using Content.Server.Roles;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Heretic;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Nutrition.AnimalHusbandry;
using Content.Shared.Nutrition.Components;
using Content.Shared.Roles;
using Robust.Shared.Audio;

namespace Content.Server.Heretic.EntitySystems;

public sealed partial class GhoulSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedRoleSystem _role = default!;
    [Dependency] private readonly MobThresholdSystem _threshold = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    public void GhoulifyEntity(Entity<GhoulComponent> ent)
    {
        RemComp<RespiratorComponent>(ent);
        RemComp<BarotraumaComponent>(ent);
        RemComp<HungerComponent>(ent);
        RemComp<ThirstComponent>(ent);
        RemComp<ReproductiveComponent>(ent);
        RemComp<ReproductivePartnerComponent>(ent);

        var hasMind = _mind.TryGetMind(ent, out var mindId, out var mind);
        if (hasMind && ent.Comp.BoundHeretic != null)
            SendBriefing(ent, mindId, mind);

        if (TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
        {
            // make them "have no eyes" and grey
            // this is clearly a reference to grey tide
            var greycolor = Color.FromHex("#505050");
            _humanoid.SetSkinColor(ent, greycolor, true, false, humanoid);
            _humanoid.SetBaseLayerColor(ent, HumanoidVisualLayers.Eyes, greycolor, true, humanoid);
        }

        _rejuvenate.PerformRejuvenate(ent);
        _threshold.SetMobStateThreshold(ent, ent.Comp.TotalHealth, Shared.Mobs.MobState.Dead);

        MakeSentientCommand.MakeSentient(ent, EntityManager);

        if (!HasComp<GhostRoleMobSpawnerComponent>(ent) && !hasMind)
        {
            var ghostRole = EnsureComp<GhostRoleComponent>(ent);
            EnsureComp<GhostTakeoverAvailableComponent>(ent);
            ghostRole.RoleName = Loc.GetString("ghostrole-ghoul-name");
            ghostRole.RoleDescription = Loc.GetString("ghostrole-ghoul-desc");
            ghostRole.RoleRules = Loc.GetString("ghostrole-ghoul-rules");
        }

        _faction.ClearFactions((ent, null));
        _faction.AddFaction((ent, null), "Heretic");
    }

    private void SendBriefing(Entity<GhoulComponent> ent, EntityUid mindId, MindComponent? mind)
    {
        var brief = Loc.GetString("heretic-ghoul-greeting-noname");

        if (ent.Comp.BoundHeretic != null)
            brief = Loc.GetString("heretic-ghoul-greeting", ("ent", Identity.Entity((EntityUid) ent.Comp.BoundHeretic, EntityManager)));
        var sound = new SoundPathSpecifier("/Audio/Goobstation/Heretic/Ambience/Antag/Heretic/heretic_gain.ogg");
        _antag.SendBriefing(ent, brief, Color.MediumPurple, sound);

        if (!_mind.TryGetRole<GhoulRoleComponent>(ent, out _))
            _role.MindAddRole<GhoulRoleComponent>(mindId, new(), mind);

        if (!_mind.TryGetRole<RoleBriefingComponent>(ent, out var rolebrief))
            _role.MindAddRole(mindId, new RoleBriefingComponent() { Briefing = brief }, mind);
        else rolebrief.Briefing += $"\n{brief}";
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhoulComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<GhoulComponent, AttackAttemptEvent>(OnTryAttack);
        SubscribeLocalEvent<GhoulComponent, TakeGhostRoleEvent>(OnTakeGhostRole);
        SubscribeLocalEvent<GhoulComponent, ExaminedEvent>(OnExamine);
    }

    private void OnInit(Entity<GhoulComponent> ent, ref ComponentInit args)
    {
        foreach (var look in _lookup.GetEntitiesInRange<HereticComponent>(Transform(ent).Coordinates, 1.5f))
        {
            if (ent.Comp.BoundHeretic == null)
                ent.Comp.BoundHeretic = look;
            else break;
        }

        GhoulifyEntity(ent);
    }
    private void OnTakeGhostRole(Entity<GhoulComponent> ent, ref TakeGhostRoleEvent args)
    {
        var hasMind = _mind.TryGetMind(ent, out var mindId, out var mind);
        if (hasMind)
            SendBriefing(ent, mindId, mind);
    }

    private void OnTryAttack(Entity<GhoulComponent> ent, ref AttackAttemptEvent args)
    {
        // prevent attacking owner and other heretics
        if (args.Target == ent.Owner
        || HasComp<HereticComponent>(args.Target))
            args.Cancel();
    }

    private void OnExamine(Entity<GhoulComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("examine-system-cant-see-entity"));
    }
}
