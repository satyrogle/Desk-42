// ============================================================
// DESK 42 — FMOD Studio authoring automation (D1 steps 6-7)
//
// Run headless against the tracked Studio project:
//
//   fmodstudiocl -script tools/fmod/studio-scripts/desk42-technical-pipeline.js \
//                FMODAssets/Desk42/Desk42.fspro
//
// Normally invoked through tools/fmod/Build-FmodBanks.ps1, which passes the
// right paths and interprets the exit status.
//
// WHAT THIS BUILDS
//   event:/Desk/Interaction   one-shot, backed by the NON-PRODUCTION
//                             technical tone, in bank "Desk42_Technical".
//
// WHAT THIS DELIBERATELY DOES NOT BUILD
//   event:/Proof/EliasRegistration18A remains ABSENT. It is an intentionally
//   unfilled production slot awaiting the authored Venn identity from
//   AudioLab. It is not stubbed, not aliased and not backed by the technical
//   tone — an absent event resolves to UnknownEvent at the backend, which is
//   the honest state. Creating an empty event here would make an unfinished
//   slot look finished.
//
//   No other event paths in the catalog are authored. Their absence is what
//   exercises the UnknownEvent diagnostic path.
//
// The script is IDEMPOTENT: re-running finds existing objects instead of
// creating duplicates, so it is safe to run on every build.
//
// Uses only documented FMOD Studio 2.03 scripting API calls. Nothing here
// reads or writes .fspro / Metadata files directly.
// ============================================================

var ASSET_FILE_NAME = "TECH_PIPELINE_TEST_NONPRODUCTION.wav";
var ASSET_REL_PATH  = "tools/fmod/assets/" + ASSET_FILE_NAME;

var EVENT_FOLDER_NAME = "Desk";
var EVENT_NAME        = "Interaction";
var EVENT_PATH        = "event:/" + EVENT_FOLDER_NAME + "/" + EVENT_NAME;

var BANK_NAME = "Desk42_Technical";

// Must cover the 0.40 s technical tone. The instrument is trimmed to the
// asset, so an over-long region would only add trailing silence.
var INSTRUMENT_LENGTH_SECONDS = 1.0;

// ------------------------------------------------------------
// Logging. fmodstudiocl surfaces console.log on stdout, which is what the
// PowerShell wrapper greps for the RESULT line.
// ------------------------------------------------------------

function log(msg)  { console.log("[desk42] " + msg); }
function warn(msg) { console.log("[desk42] WARN  " + msg); }

function fail(msg) {
    console.log("[desk42] FAIL  " + msg);
    console.log("[desk42] RESULT FAILED");
    throw new Error(msg);
}

// Dumps what a managed object actually exposes. Called only on the paths
// where the API surface is the thing in doubt, so a bad assumption produces
// a usable diagnostic instead of a bare 'undefined is not an object'.
function describe(obj, label) {
    try {
        var names = [];
        for (var k in obj.relationships) { names.push(k); }
        warn(label + " relationships: " + names.join(", "));
    } catch (e) {
        warn(label + ": could not enumerate relationships (" + e + ")");
    }
}

// ------------------------------------------------------------
// Path resolution
// ------------------------------------------------------------

function parentDir(path) {
    var normalised = path.replace(/\\/g, "/");
    return normalised.substring(0, normalised.lastIndexOf("/"));
}

// <repo>/FMODAssets/Desk42/Desk42.fspro -> <repo>
function repositoryRoot() {
    var dir = parentDir(studio.project.filepath); // <repo>/FMODAssets/Desk42
    for (var i = 0; i < 2; i++) { dir = parentDir(dir); }
    return dir;
}

// ------------------------------------------------------------
// Steps
// ------------------------------------------------------------

function importTechnicalAsset(wavPath) {
    var existing = studio.project.workspace.masterAssetFolder.getAsset(ASSET_FILE_NAME);
    if (existing) {
        log("asset already imported: " + ASSET_FILE_NAME);
        return existing;
    }

    if (!studio.system.getFile(wavPath).exists()) {
        fail("technical asset not found at " + wavPath +
             " — run tools/fmod/New-TechnicalTestAsset.ps1 first.");
    }

    log("importing " + wavPath);
    var imported = studio.project.importAudioFile(wavPath);
    if (!imported) {
        fail("importAudioFile returned null for " + wavPath);
    }

    // importAudioFile always lands in the root audio bin; re-reading through
    // getAsset is what the instrument assignment below needs anyway.
    var asset = studio.project.workspace.masterAssetFolder.getAsset(ASSET_FILE_NAME);
    if (!asset) {
        fail("imported " + ASSET_FILE_NAME + " but it is not resolvable in the master asset folder.");
    }
    return asset;
}

function ensureEventFolder(name) {
    var existing = studio.project.workspace.masterEventFolder.getItem(name);
    if (existing) {
        log("event folder exists: " + name);
        return existing;
    }

    log("creating event folder: " + name);
    var folder = studio.project.create("EventFolder");
    folder.name = name;
    folder.folder = studio.project.workspace.masterEventFolder;
    return folder;
}

function ensureEvent(folder, name) {
    var existing = studio.project.lookup(EVENT_PATH);
    if (existing) {
        log("event exists: " + EVENT_PATH);
        return existing;
    }

    log("creating event: " + EVENT_PATH);
    var event = studio.project.create("Event");
    event.name = name;
    event.folder = folder;
    return event;
}

// True when the event's master track already carries an instrument backed by
// our asset, so a re-run does not stack duplicate instruments on the timeline.
function eventAlreadyHasAsset(event, asset) {
    try {
        var modules = event.masterTrack.modules;
        for (var i = 0; i < modules.length; i++) {
            var m = modules[i];
            if (m.isOfType && m.isOfType("SingleSound") && m.audioFile &&
                m.audioFile.id === asset.id) {
                return true;
            }
        }
    } catch (e) {
        warn("could not inspect existing instruments (" + e + "); assuming none.");
    }
    return false;
}

function ensureInstrument(event, asset) {
    if (eventAlreadyHasAsset(event, asset)) {
        log("instrument already present on " + EVENT_PATH);
        return;
    }

    log("adding single-sound instrument to " + EVENT_PATH);
    var instrument = event.masterTrack.addSound(
        event.timeline, "SingleSound", 0, INSTRUMENT_LENGTH_SECONDS);

    if (!instrument) {
        fail("addSound returned null on the master track of " + EVENT_PATH);
    }
    instrument.audioFile = asset;
}

function ensureBank(name) {
    var existing = studio.project.lookup("bank:/" + name);
    if (existing) {
        log("bank exists: " + name);
        return existing;
    }

    log("creating bank: " + name);
    var bank = studio.project.create("Bank");
    bank.name = name;

    // Banks live under the master bank folder. Older/newer schemas have
    // differed here, so fall back to the project root rather than aborting a
    // build over folder placement.
    try {
        if (studio.project.workspace.masterBankFolder) {
            bank.folder = studio.project.workspace.masterBankFolder;
        }
    } catch (e) {
        warn("could not place bank under masterBankFolder (" + e + "); left at root.");
    }
    return bank;
}

function assignEventToBank(event, bank) {
    try {
        var banks = event.relationships.banks;
        for (var i = 0; i < banks.size(); i++) {
            if (banks.destination(i).id === bank.id) {
                log("event already assigned to bank " + bank.name);
                return;
            }
        }
        banks.add(bank);
        log("assigned " + EVENT_PATH + " -> bank:/" + bank.name);
    } catch (e) {
        describe(event, "Event");
        fail("could not assign event to bank: " + e);
    }
}

// ------------------------------------------------------------
// Main
// ------------------------------------------------------------

log("Studio project: " + studio.project.filepath);

var root    = repositoryRoot();
var wavPath = root + "/" + ASSET_REL_PATH;
log("repository root: " + root);

var asset  = importTechnicalAsset(wavPath);
var folder = ensureEventFolder(EVENT_FOLDER_NAME);
var event  = ensureEvent(folder, EVENT_NAME);
ensureInstrument(event, asset);

var bank = ensureBank(BANK_NAME);
assignEventToBank(event, bank);

log("saving project");
studio.project.save();

log("building banks");
var built = studio.project.build();
if (!built) {
    fail("studio.project.build() reported failure.");
}

// ------------------------------------------------------------
// Post-build verification. Confirms the authored state is what we claim,
// and re-asserts that the Elias slot was NOT filled by this run.
// ------------------------------------------------------------

var resolved = studio.project.lookup(EVENT_PATH);
if (!resolved) {
    fail("post-build lookup failed for " + EVENT_PATH);
}
log("verified event resolves: " + EVENT_PATH);

var eliasSlot = studio.project.lookup("event:/Proof/EliasRegistration18A");
if (eliasSlot) {
    warn("event:/Proof/EliasRegistration18A EXISTS in the project. " +
         "This script did not create it. The causal identity is supposed to " +
         "remain an unfilled slot until AudioLab delivers authored audio.");
} else {
    log("confirmed event:/Proof/EliasRegistration18A is absent (unfilled slot, as intended)");
}

log("RESULT OK event=" + EVENT_PATH + " bank=" + BANK_NAME + " asset=" + ASSET_FILE_NAME);
