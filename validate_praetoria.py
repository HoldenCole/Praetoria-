#!/usr/bin/env python3
"""
Praetoria content validator (pre-flight, before `dotnet run --project src/Tools -- validate`).

Replicates the engine's documented load-time gates from CONTENT_GUIDE.md so authors catch
errors locally. Checks events (logic+text pairing, tokens, vocab, flag-chains), holdings
(ids, resources, requires-targets, §17 dominance), and scenarios (refs, bonds, vocab).

USAGE:
    python3 validate_praetoria.py                # validates split files under ./content/
    python3 validate_praetoria.py bundle.json    # validates a single merged bundle

Both modes tolerate JSONC (// and /* */ comments, trailing commas) — the same dialect the
engine's loader accepts. Exit code 0 = clean, 1 = errors. Run after every batch.
"""
import json, re, sys, glob, os

COND={"academy","cohort","all","any","not","const","worldFlag","charFlag","relationship","bond","skill","trait","rank","turn","counter","resource","sphere","title","claim","age","track","era","fleetStrength","atWar","bloc","sex","eventFired","wealth","deposit"}
EFF={"setWorldFlag","setCharFlag","adjustRelationship","addBond","adjustSkill","adjustStress","adjustCounter","addTrait","advanceCareer","adjustResource","grantClaim","revokeClaim","adjustLegitimacy","setTitle","succeed","kill","declareWar","makePeace","joinBloc","log"}
TIERS={"ambient","situation","setpiece"}; POOLS={"influence","treasury","agents"}
RES={"credits","materials","manpower","influence","exotics","helium3","rare_earths","isotopes"}; BONDS={"none","blood","sworn","marriage"}; ERAS={"fractured_stars","imperium"}
SPHERES={"navy","treasury","senate"}; TITLES={"landless","knight","baron","count","duke","archduke","emperor"}
# §4b: engine-written projections of the player's house — gate on them, but never adjustCounter them.
READONLY_COUNTERS={"title_rank","house_legitimacy","title_instability"}

# === §8 LOCKED CONTROLLED VOCABULARIES (canonical bible — see docs/PRAETORIA_VOCAB.md) ===
NATURE={"Ambitious","Arrogant","Cruel","Honorable","Just","Loyal","Proud","Ruthless","Vengeful"}
APTITUDE={"Administrator","Brilliant","Diplomat","Duelist","Greedy","Orator","Paranoid","Quartermaster","Schemer","Strategist","Tactician"}
SKILLS={"administration","charisma","diplomacy","discipline","economics","engineering","gunnery","intrigue","law","leadership","logistics","oratory","tactics"}
AMBITIONS={"amass_a_fortune","command_a_fleet","earn_a_command","master_the_senate","outshine_all_rivals","restore_house_fortunes","seize_the_throne","win_a_great_love"}
TRACKS={"military","stewardship","law"}
# pre-existing catalog ids the guide ships (a bundle of NEW content must not redefine them; in
# split mode they are loaded as part of the corpus, so the guard is bundle-only — see validate_holdings)
EXIST_SPECS={"agri_world","forge_world","trade_hub","fortress","research_station"}
EXIST_BUILDS={"power_plant","mine","farm_complex","market"}

# True when validating a single merged bundle (arg given); False when scanning ./content/.
BUNDLE = len(sys.argv) > 1

errors=[]; warns=[]

def _read_jsonc(path):
    """Parse JSON that may carry // and /* */ comments and trailing commas (string-aware, so a
    "//" used as an object KEY or inside a value is preserved)."""
    s=open(path,encoding="utf-8").read()
    out=[]; i=0; n=len(s); instr=False; esc=False
    while i<n:
        c=s[i]
        if instr:
            out.append(c)
            if esc: esc=False
            elif c=="\\": esc=True
            elif c=='"': instr=False
            i+=1; continue
        if c=='"': instr=True; out.append(c); i+=1; continue
        if c=="/" and i+1<n and s[i+1]=="/":
            while i<n and s[i]!="\n": i+=1
            continue
        if c=="/" and i+1<n and s[i+1]=="*":
            i+=2
            while i+1<n and not (s[i]=="*" and s[i+1]=="/"): i+=1
            i+=2; continue
        out.append(c); i+=1
    t="".join(out)
    t=re.sub(r",(\s*[}\]])", r"\1", t)   # drop trailing commas
    return json.loads(t)

def load_corpus():
    """Returns (events, texts{by id}, specs[], builds[], scenarios[])."""
    if BUNDLE:
        b=_read_jsonc(sys.argv[1]).get("praetoria_content_bundle",{})
        return (b.get("events",[]), {t["id"]:t for t in b.get("texts",[])},
                b.get("specializations",[]), b.get("buildings",[]), b.get("scenarios",[]))
    ev=[]; tx={}; sp=[]; bd=[]; sc=[]
    for f in glob.glob("content/events/*.json"): ev+=_read_jsonc(f).get("events",[])
    for f in glob.glob("content/text/*.json"):
        for t in _read_jsonc(f).get("texts",[]): tx[t["id"]]=t
    for f in glob.glob("content/holdings/*.json"):
        d=_read_jsonc(f); sp+=d.get("specializations",[]); bd+=d.get("buildings",[])
    for f in glob.glob("content/scenarios/*.json"): sc.append(_read_jsonc(f))
    return ev,tx,sp,bd,sc

def walk(conds,ctx,roles):
    for c in conds:
        if c.get("type") not in COND: errors.append(f"[{ctx}] bad condition '{c.get('type')}'")
        if c.get("type")=="trait":
            tr=c.get("trait"); kind=c.get("kind","any")
            if kind=="nature" and tr not in NATURE: errors.append(f"[{ctx}] unlocked nature trait '{tr}'")
            if kind=="aptitude" and tr not in APTITUDE: errors.append(f"[{ctx}] unlocked aptitude trait '{tr}'")
            if kind=="any" and tr not in NATURE|APTITUDE: errors.append(f"[{ctx}] unknown trait '{tr}'")
        if c.get("type")=="skill" and c.get("skill") not in SKILLS:
            errors.append(f"[{ctx}] unlocked skill '{c.get('skill')}'")
        if c.get("type")=="era" and c.get("era") not in ERAS:
            errors.append(f"[{ctx}] bad era '{c.get('era')}'")
        if c.get("type")=="track" and c.get("track") not in TRACKS:
            errors.append(f"[{ctx}] unlocked track '{c.get('track')}'")
        if c.get("type")=="sphere" and c.get("sphere") not in SPHERES:
            errors.append(f"[{ctx}] bad sphere '{c.get('sphere')}'")
        if c.get("type") in ("title","claim") and c.get("title") not in TITLES:
            errors.append(f"[{ctx}] bad title id '{c.get('title')}'")
        if "of" in c:
            v=c["of"]; walk(v if isinstance(v,list) else [v],ctx,roles)
        for rf in ("role","from","to"):
            if rf in c and c[rf] not in roles: errors.append(f"[{ctx}] unbound role '{c[rf]}'")
        if "vsRole" in c and c["vsRole"] not in roles: errors.append(f"[{ctx}] unbound vsRole '{c['vsRole']}'")

def check_tokens(text,roles,ctx):
    for role,field in re.findall(r"\{(\w+)\.(\w+)\}",text):
        if role not in roles: errors.append(f"[{ctx}] token role '{role}' undeclared")
        if field not in ("name","house","rank","rankName","title"): errors.append(f"[{ctx}] token bad field '{field}'")
    # {era:Republic wording|Imperium wording} — exactly two plain alternatives, no nested tokens (docs/TITLES.md)
    for body in re.findall(r"\{era:([^}]*)\}",text):
        if body.count("|")!=1 or "{" in body: errors.append(f"[{ctx}] malformed era token '{{era:{body}}}' (want {{era:A|B}}, no nesting)")
    if re.search(r"\{era:[^}]*$",text): errors.append(f"[{ctx}] unclosed era token")
    for bare in re.findall(r"\{(\w+)\}",text):
        errors.append(f"[{ctx}] malformed token '{{{bare}}}' (missing .field)")

def validate_events(ev,tx):
    ids=set(); written=set(); read=set()
    for e in ev:
        eid=e["id"]
        if eid in ids: errors.append(f"DUPLICATE event id '{eid}'")
        ids.add(eid)
        if e.get("tier","situation") not in TIERS: errors.append(f"[{eid}] bad tier")
        roles={"self"}
        for r in e.get("roles",[]):
            roles.add(r["name"]); walk(r.get("when",[]),f"{eid}/role:{r['name']}",roles)
        walk(e.get("when",[]),f"{eid}/when",roles)
        for a in e.get("ambitions",[]):
            if a not in AMBITIONS: errors.append(f"[{eid}] unlocked ambition tag '{a}'")
        if eid not in tx: errors.append(f"[{eid}] NO TEXT RECORD"); continue
        t=tx[eid]
        if not t.get("title") or not t.get("body"): errors.append(f"[{eid}] missing title/body")
        check_tokens(json.dumps(t),roles,eid)
        cids=set()
        for ch in e.get("choices",[]):
            cid=ch["id"]; cids.add(cid)
            if cid not in t.get("choices",{}): errors.append(f"[{eid}/{cid}] NO CHOICE TEXT")
            for k in ch.get("cost",{}):
                if k not in POOLS: errors.append(f"[{eid}/{cid}] bad pool '{k}'")
            walk(ch.get("requires",[]),f"{eid}/{cid}/req",roles)
            for ef in ch.get("effects",[]):
                if ef["type"] not in EFF: errors.append(f"[{eid}/{cid}] bad effect '{ef['type']}'")
                for rf in ("role","from","to"):
                    if rf in ef and ef[rf] not in roles: errors.append(f"[{eid}/{cid}] effect unbound role '{ef[rf]}'")
                if ef["type"]=="adjustResource" and ef.get("resource") not in RES: errors.append(f"[{eid}/{cid}] bad resource")
                if ef["type"]=="addTrait":
                    tr=ef.get("trait"); kind=ef.get("kind","aptitude")
                    pool=NATURE if kind=="nature" else APTITUDE
                    if tr not in pool: errors.append(f"[{eid}/{cid}] addTrait unlocked {kind} '{tr}'")
                if ef["type"]=="adjustSkill" and ef.get("skill") not in SKILLS:
                    errors.append(f"[{eid}/{cid}] adjustSkill unlocked skill '{ef.get('skill')}'")
                if ef["type"] in ("grantClaim","revokeClaim","setTitle") and ef.get("title") not in TITLES:
                    errors.append(f"[{eid}/{cid}] {ef['type']} bad title id '{ef.get('title')}'")
                if ef["type"]=="addBond" and ef.get("bond") not in BONDS:
                    errors.append(f"[{eid}/{cid}] addBond bad bond '{ef.get('bond')}'")
                if ef["type"]=="adjustCounter" and ef.get("key") in READONLY_COUNTERS:
                    errors.append(f"[{eid}/{cid}] adjustCounter on READ-ONLY counter '{ef.get('key')}' "
                                  f"(engine-written; use setTitle/adjustLegitimacy/grantClaim)")
                if ef["type"]=="log": check_tokens(ef.get("text",""),roles,f"{eid}/{cid}/log")
                if ef["type"] in ("setCharFlag","setWorldFlag"): written.add(ef["flag"])
                if "//" in ef: errors.append(f"[{eid}/{cid}] stray '//' key inside effect")
        if e.get("choices") and all(ch.get("requires") for ch in e["choices"]):
            errors.append(f"[{eid}] every choice is gated by `requires` — leave one ungated or the event can be un-actionable and is re-offered every turn")
        orphan=set(t.get("choices",{}))-cids
        if orphan: errors.append(f"[{eid}] orphan choice text {orphan}")
        def collect(conds):
            for c in conds:
                if c.get("type") in ("charFlag","worldFlag"): read.add(c["flag"])
                if "of" in c: collect(c["of"] if isinstance(c["of"],list) else [c["of"]])
        collect(e.get("when",[]))
        for r in e.get("roles",[]): collect(r.get("when",[]))
    for tid in tx:
        if tid not in ids: errors.append(f"orphan text record '{tid}'")
    dead=read-written
    # engine-managed/scenario-seeded flags are legitimately read-only here:
    dead={f for f in dead if not f.startswith("evt:")}
    if dead: warns.append(f"flags read but never written in events (ok if scenario-seeded or engine-set): {sorted(dead)}")
    unread=written-read
    if unread: warns.append(f"flags written but never read by any event (dead-end chains; fine for markers/future hooks): {sorted(unread)}")
    return ids

def validate_holdings(sp,bd):
    specs={}; builds={}
    for s in sp:
        # In split mode the base catalog is part of the corpus; only flag a TRUE duplicate.
        if s["id"] in specs or (BUNDLE and s["id"] in EXIST_SPECS): errors.append(f"dup spec id '{s['id']}'")
        specs[s["id"]]=s
        for grp in ("yield","upkeep"):
            for k in s.get(grp,{}):
                if k not in RES: errors.append(f"spec {s['id']}.{grp} bad resource '{k}'")
    for b in bd:
        if b["id"] in builds or (BUNDLE and b["id"] in EXIST_BUILDS): errors.append(f"dup building id '{b['id']}'")
        builds[b["id"]]=b
        for grp in ("cost","yield","upkeep"):
            for k in b.get(grp,{}):
                if k not in RES: errors.append(f"building {b['id']}.{grp} bad resource '{k}'")
    allspecs=set(specs)|EXIST_SPECS
    for bid,b in builds.items():
        if "requires" in b and b["requires"] not in allspecs:
            errors.append(f"building {bid} requires unknown spec '{b['requires']}'")
    return specs,builds

HOLDING_KINDS={"planet","station","claim"}
PLANET_CLASSES={"terran","arid","ocean","forge","frozen","gas","belt","barren"}

def _load_galaxies():
    """Galaxies (systems + regions + lanes) and the minor-house baselines that share the directory."""
    gal={}
    for f in glob.glob("content/galaxy/*.json"):
        g=_read_jsonc(f)
        if "systems" not in g: continue          # a baseline (minor houses) file — validated with the scenarios
        ids={s["id"] for s in g.get("systems",[])}
        regions={r["id"] for r in g.get("regions",[])}
        for r in g.get("regions",[]):
            if r.get("ring","middle") not in {"core","middle","rim"}: errors.append(f"galaxy {g.get('id')} region {r['id']} unknown ring '{r.get('ring')}'")
        if g.get("capital") and g["capital"] not in ids: errors.append(f"galaxy {g.get('id')} capital '{g['capital']}' is not a system")
        for ring,rules in (g.get("rings") or {}).items():
            if ring=="//": continue
            if ring not in {"core","middle","rim"}: errors.append(f"galaxy {g.get('id')} rings: unknown ring '{ring}'")
            for k in rules:
                if k not in {"prosperity","unrestDrift","lawlessnessDrift","lawlessnessStart","allegiance","tradeBonus","charterBonus","//"}: errors.append(f"galaxy {g.get('id')} rings.{ring}: unknown rule '{k}'")
        if g.get("systems") and not g.get("capital"): warns.append(f"galaxy {g.get('id')} names no capital")
        for s in g.get("systems",[]):
            if s.get("region") and s["region"] not in regions: errors.append(f"galaxy {g.get('id')} system {s['id']} in unknown region '{s['region']}'")
            if s.get("planet","terran") not in PLANET_CLASSES: errors.append(f"galaxy {g.get('id')} system {s['id']} unknown planet class '{s.get('planet')}'")
            if not isinstance(s.get("belt",0),int) or not 0 <= s.get("belt",0) <= 5: errors.append(f"galaxy {g.get('id')} system {s['id']} belt must be 0..5")
            bodies=s.get("bodies",[])
            if bodies:
                prim=[b for b in bodies if b.get("primary")]
                if len(prim)!=1: errors.append(f"galaxy {g.get('id')} system {s['id']} needs exactly one primary body")
                elif prim[0].get("class")!=s.get("planet","terran"): errors.append(f"galaxy {g.get('id')} system {s['id']} primary body class differs from the system's planet")
                for b in bodies:
                    if b.get("class","barren") not in PLANET_CLASSES: errors.append(f"galaxy {g.get('id')} system {s['id']} body {b.get('name')} unknown class '{b.get('class')}'")
            else: warns.append(f"galaxy {g.get('id')} system {s['id']} lists no bodies")
        deg={i:0 for i in ids}
        for a,b in g.get("lanes",[]):
            if a in deg: deg[a]+=1
            if b in deg: deg[b]+=1
        if ids and max(deg.values())>5: warns.append(f"galaxy {g.get('id')}: a system has more than five lanes; the web is meant to be sparse")
        if ids and min(deg.values())<1: errors.append(f"galaxy {g.get('id')}: a system has no lane")
        for r in regions:
            if not any(s.get("region")==r for s in g.get("systems",[])): errors.append(f"galaxy {g.get('id')} region {r} has no systems")
        for a,b in g.get("lanes",[]):
            if a not in ids or b not in ids: errors.append(f"galaxy {g.get('id')} lane {a}-{b} references an unknown system")
        if g.get("baseline") and not os.path.exists(f"content/galaxy/{g['baseline']}.json"): errors.append(f"galaxy {g.get('id')} names missing baseline '{g['baseline']}'")
        gal[g["id"]]=ids
    return gal

def _baselines():
    out=[]
    for f in glob.glob("content/galaxy/*.json"):
        g=_read_jsonc(f)
        if "systems" in g: continue
        g.setdefault("protagonist", None); g.setdefault("era","fractured_stars"); g["galaxy"]=None; g["_baseline"]=True
        out.append(g)
    return out

def validate_creation():
    """traits.json must BE the locked lists; backgrounds may only use locked vocabulary."""
    for f in glob.glob("content/traits/*.json"):
        t=_read_jsonc(f)
        for key,locked in (("nature",NATURE),("aptitude",APTITUDE),("ambitions",AMBITIONS)):
            if key not in t: continue
            ids={x["id"] for x in t.get(key,[])}
            if ids!=locked:
                errors.append(f"{f} '{key}' differs from the locked vocabulary: missing {sorted(locked-ids)}, extra {sorted(ids-locked)}")
        for act,row in t.get("friction",{}).items():
            for tr in row:
                if tr not in NATURE|APTITUDE: errors.append(f"friction '{act}': unlocked trait '{tr}'")
    for f in glob.glob("content/honors/*.json"):
        for h in _read_jsonc(f).get("honors",[]):
            walk(h.get("when",[]),f"honor:{h.get('id')}",{"self"})
            for ef in h.get("effects",[]):
                if ef.get("type") not in EFF: errors.append(f"honor {h.get('id')}: bad effect '{ef.get('type')}'")
    for f in glob.glob("content/great_crisis/*.json"):
        g=_read_jsonc(f).get("greatCrisis",{})
        for st in g.get("stages",[]):
            for ef in st.get("effects",[]):
                if ef.get("type") not in EFF: errors.append(f"great crisis stage {st.get('id')}: bad effect '{ef.get('type')}'")
            if st.get("behavior","") not in ("","blocs","bloc_war","climax","resolution"): errors.append(f"great crisis stage {st.get('id')}: unknown behavior")
    for f in glob.glob("content/fleets/*.json"):
        d=_read_jsonc(f)
        for c in d.get("classes",[]):
            for grp in ("cost","upkeep"):
                for k in c.get(grp,{}):
                    if k not in RES: errors.append(f"ship class {c.get('id')}.{grp} bad resource '{k}'")
    for f in glob.glob("content/backgrounds/*.json"):
        for b in _read_jsonc(f).get("backgrounds",[]):
            bid=b.get("id","?")
            for t in b.get("nature",[]):
                if t not in NATURE: errors.append(f"background {bid}: unlocked nature '{t}'")
            for t in b.get("aptitude",[]):
                if t not in APTITUDE: errors.append(f"background {bid}: unlocked aptitude '{t}'")
            for s in b.get("skills",{}):
                if s not in SKILLS: errors.append(f"background {bid}: unlocked skill '{s}'")
            for k in b.get("treasury",{}):
                if k not in RES: errors.append(f"background {bid}: bad treasury key '{k}'")
            if b.get("trackHint") and b["trackHint"] not in TRACKS: errors.append(f"background {bid}: unlocked trackHint")
            if b.get("ambitionHint") and b["ambitionHint"] not in AMBITIONS: errors.append(f"background {bid}: unlocked ambitionHint")

def validate_scenarios(sc,allspecs):
    vocab={"nature":set(),"aptitude":set(),"skills":set(),"ambition":set(),"track":set()}
    galaxies=_load_galaxies() if not BUNDLE else {}
    for d in sc:
        sid=d.get("id","?")
        gid=d.get("galaxy")
        if gid and not BUNDLE:
            if gid not in galaxies: errors.append(f"[{sid}] unknown galaxy '{gid}'")
            else:
                for h in d.get("holdings",[]):
                    if h.get("system") and h["system"] not in galaxies[gid]: errors.append(f"[{sid}] holding {h['id']} in unknown system '{h['system']}'")
                for f in d.get("fleets",[]):
                    if f.get("system") not in galaxies[gid]: errors.append(f"[{sid}] fleet {f.get('id')} in unknown system '{f.get('system')}'")
                    if f.get("owner") not in {h["id"] for h in d.get("houses",[])}: errors.append(f"[{sid}] fleet {f.get('id')} has unknown owner '{f.get('owner')}'")
                for b in d.get("businesses",[]):
                    if b.get("system") not in galaxies[gid]: errors.append(f"[{sid}] venture {b.get('id')} in unknown system '{b.get('system')}'")
                    if b.get("owner") not in {h["id"] for h in d.get("houses",[])}: errors.append(f"[{sid}] venture {b.get('id')} owned by unknown house '{b.get('owner')}'")
        chars={c["id"] for c in d.get("characters",[])}
        # a scenario laid over a galaxy also has that galaxy's standing houses and their people (the baseline the loader merges in)
        if gid and not BUNDLE:
            try:
                base=json.load(open(os.path.join("content","galaxy",gid+"_houses.json")))
                chars |= {c["id"] for c in base.get("characters",[])}
            except Exception: pass
        houses={h["id"] for h in d.get("houses",[])}
        for h in d.get("holdings",[]):
            if h.get("kind","planet") not in HOLDING_KINDS: errors.append(f"[{sid}] holding {h['id']} unknown kind '{h.get('kind')}'")
            if h.get("governor") and h["governor"] not in chars: errors.append(f"[{sid}] holding {h['id']} governor '{h['governor']}' is not a character")
            if h.get("governor") and next((c for c in d.get("characters",[]) if c["id"]==h["governor"]),{}).get("house")!=h.get("owner"): errors.append(f"[{sid}] holding {h['id']} governor is not of the owning house")
            if h.get("owner") not in houses: errors.append(f"[{sid}] holding {h['id']} unknown owner '{h.get('owner')}'")
        if d.get("_baseline"):
            seen={}
            for h in d.get("holdings",[]):
                if h.get("kind","planet")=="planet": 
                    if h["system"] in seen: errors.append(f"[{sid}] two baseline planets in system {h['system']}")
                    seen[h["system"]]=h["id"]
        if not d.get("_baseline") and d.get("protagonist") not in chars: errors.append(f"[{sid}] protagonist not in characters")
        if d.get("era") not in ERAS: errors.append(f"[{sid}] bad era '{d.get('era')}'")
        for h in d.get("houses",[]):
            for k in h.get("treasury",{}):
                if k not in RES: errors.append(f"[{sid}] house {h['id']} bad treasury key '{k}'")
        for c in d.get("characters",[]):
            if c["house"] not in houses: errors.append(f"[{sid}] char {c['id']} unknown house")
            if c.get("careerTrack") not in TRACKS: errors.append(f"[{sid}] char {c['id']} unlocked track '{c.get('careerTrack')}'")
            amb=c.get("ambition")
            if (amb or c.get("age",18)>=16) and amb not in AMBITIONS: errors.append(f"[{sid}] char {c['id']} unlocked ambition '{amb}'")   # children choose at sixteen
            vocab["track"].add(c.get("careerTrack")); vocab["ambition"].add(c.get("ambition"))
            for t in c.get("nature",[]):
                if t not in NATURE: errors.append(f"[{sid}] char {c['id']} unlocked nature '{t}'")
                vocab["nature"].add(t)
            for t in c.get("aptitude",[]):
                if t not in APTITUDE: errors.append(f"[{sid}] char {c['id']} unlocked aptitude '{t}'")
                vocab["aptitude"].add(t)
            for sk in c.get("skills",{}):
                if sk not in SKILLS: errors.append(f"[{sid}] char {c['id']} unlocked skill '{sk}'")
                vocab["skills"].add(sk)
        for r in d.get("relationships",[]):
            for end in ("from","to"):
                if r[end] not in chars: errors.append(f"[{sid}] relationship {end} not a character")
            if r.get("bond","none") not in BONDS: errors.append(f"[{sid}] bad bond '{r.get('bond')}'")
            if not (-100<=r.get("disposition",0)<=100): errors.append(f"[{sid}] disposition out of range")
        for h in d.get("holdings",[]):
            if h["owner"] not in houses: errors.append(f"[{sid}] holding unknown owner")
            if h["specialization"] not in allspecs: errors.append(f"[{sid}] holding unknown spec '{h['specialization']}'")
    return vocab

def validate_businesses(allspecs):
    """content/businesses: ids, resource keys, requirement targets, finance rules."""
    n=0
    for f in glob.glob("content/businesses/*.json"):
        d=_read_jsonc(f); seen=set()
        for b in d.get("businesses",[]):
            n+=1
            if b["id"] in seen: errors.append(f"dup business id '{b['id']}'")
            seen.add(b["id"])
            for grp in ("cost","yield","flat","upkeep","expandCost"):
                for k in b.get(grp,{}):
                    if k not in RES: errors.append(f"business {b['id']}.{grp} bad resource '{k}'")
            if not b.get("yield") and not b.get("flat"): warns.append(f"business {b['id']} yields nothing")
            req=b.get("requires",{})
            for sp_ in req.get("specializations",[]):
                if sp_ not in allspecs: errors.append(f"business {b['id']} requires unknown spec '{sp_}'")
            for pc in req.get("planetClasses",[]):
                if pc not in PLANET_CLASSES: errors.append(f"business {b['id']} requires unknown planet class '{pc}'")
            if b.get("maxLevel",3)<1: errors.append(f"business {b['id']} maxLevel < 1")
        fin=d.get("finance",{})
        if fin and fin.get("loanRepay",26)<=fin.get("loanPrincipal",20): errors.append("finance: loanRepay must exceed loanPrincipal")
    return n

def main():
    ev,tx,sp,bd,sc=load_corpus()
    validate_events(ev,tx)
    if not BUNDLE: validate_creation()
    specs,builds=validate_holdings(sp,bd)
    nbiz=validate_businesses(set(specs)|EXIST_SPECS) if not BUNDLE else 0
    vocab=validate_scenarios(sc + ([] if BUNDLE else _baselines()), set(specs)|EXIST_SPECS)

    mode="bundle" if BUNDLE else "./content"
    print(f"CORPUS ({mode}): {len(ev)} events, {len(tx)} texts, {len(sp)} specs, {len(bd)} buildings, {nbiz} ventures, {len(sc)} scenarios")
    # §17 dominance audit
    cov={max(s['yield'],key=s['yield'].get) for s in sp if s.get('yield')}
    print(f"§17 resource dominance (specs): {sorted(cov)}  | upkeep-sinks: {[s['id'] for s in sp if not s.get('yield')]}")
    print("\n§8 VOCAB USED (review against docs/PRAETORIA_VOCAB.md):")
    for k,v in vocab.items(): print(f"  {k}: {sorted(x for x in v if x)}")
    if warns:
        print("\nWARNINGS:")
        for w in warns: print("  ~",w)
    if errors:
        print(f"\n{len(errors)} ERROR(S):")
        for e in errors: print("  -",e)
        sys.exit(1)
    print("\n=== VALIDATION PASSED — 0 errors ===")

if __name__=="__main__": main()
