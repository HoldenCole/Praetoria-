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

COND={"all","any","not","const","worldFlag","charFlag","relationship","bond","skill","trait","rank","turn","counter","resource","sphere","title","claim","age","eventFired"}
EFF={"setWorldFlag","setCharFlag","adjustRelationship","addBond","adjustSkill","adjustStress","adjustCounter","addTrait","advanceCareer","adjustResource","grantClaim","adjustLegitimacy","setTitle","kill","log"}
TIERS={"ambient","situation","setpiece"}; POOLS={"influence","treasury","agents"}
RES={"credits","materials","manpower","influence","exotics"}; BONDS={"none","blood","sworn","marriage"}; ERAS={"fractured_stars","imperium"}
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
        if field not in ("name","house","rank"): errors.append(f"[{ctx}] token bad field '{field}'")
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
                if ef["type"] in ("grantClaim","setTitle") and ef.get("title") not in TITLES:
                    errors.append(f"[{eid}/{cid}] {ef['type']} bad title id '{ef.get('title')}'")
                if ef["type"]=="addBond" and ef.get("bond") not in BONDS:
                    errors.append(f"[{eid}/{cid}] addBond bad bond '{ef.get('bond')}'")
                if ef["type"]=="adjustCounter" and ef.get("key") in READONLY_COUNTERS:
                    errors.append(f"[{eid}/{cid}] adjustCounter on READ-ONLY counter '{ef.get('key')}' "
                                  f"(engine-written; use setTitle/adjustLegitimacy/grantClaim)")
                if ef["type"]=="log": check_tokens(ef.get("text",""),roles,f"{eid}/{cid}/log")
                if ef["type"] in ("setCharFlag","setWorldFlag"): written.add(ef["flag"])
                if "//" in ef: errors.append(f"[{eid}/{cid}] stray '//' key inside effect")
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

def validate_scenarios(sc,allspecs):
    vocab={"nature":set(),"aptitude":set(),"skills":set(),"ambition":set(),"track":set()}
    for d in sc:
        sid=d.get("id","?")
        chars={c["id"] for c in d.get("characters",[])}
        houses={h["id"] for h in d.get("houses",[])}
        if d.get("protagonist") not in chars: errors.append(f"[{sid}] protagonist not in characters")
        if d.get("era") not in ERAS: errors.append(f"[{sid}] bad era '{d.get('era')}'")
        for h in d.get("houses",[]):
            for k in h.get("treasury",{}):
                if k not in RES: errors.append(f"[{sid}] house {h['id']} bad treasury key '{k}'")
        for c in d.get("characters",[]):
            if c["house"] not in houses: errors.append(f"[{sid}] char {c['id']} unknown house")
            if c.get("careerTrack") not in TRACKS: errors.append(f"[{sid}] char {c['id']} unlocked track '{c.get('careerTrack')}'")
            if c.get("ambition") not in AMBITIONS: errors.append(f"[{sid}] char {c['id']} unlocked ambition '{c.get('ambition')}'")
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

def main():
    ev,tx,sp,bd,sc=load_corpus()
    validate_events(ev,tx)
    specs,builds=validate_holdings(sp,bd)
    vocab=validate_scenarios(sc, set(specs)|EXIST_SPECS)

    mode="bundle" if BUNDLE else "./content"
    print(f"CORPUS ({mode}): {len(ev)} events, {len(tx)} texts, {len(sp)} specs, {len(bd)} buildings, {len(sc)} scenarios")
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
