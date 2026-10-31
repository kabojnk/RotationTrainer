namespace RotationTrainer.GameData;

/// <summary>
/// Buttons that fire a different action depending on the job's state (Living Muse becomes Pom Muse, Fire in Red
/// becomes Aero in Green): replacement action id -> the action ids of the button(s) it appears on, so its keybind can
/// be shown before the button turns into it. Only the families the game data does not link are listed here; shared
/// cooldowns and combo chains cover the rest (see <see cref="LuminaCatalog"/>). Generated from the English Action
/// sheet for patch 7.x.
/// </summary>
internal static class ButtonHostTable
{
    public static readonly Dictionary<uint, uint[]> Hosts = new()
    {
        // PCT
        [34651] = [34650], // Aero in Green -> Fire in Red
        [34652] = [34650], // Water in Blue -> Fire in Red
        [34657] = [34656], // Aero II in Green -> Fire II in Red
        [34658] = [34656], // Water II in Blue -> Fire II in Red
        [34654] = [34653], // Stone in Yellow -> Blizzard in Cyan
        [34655] = [34653], // Thunder in Magenta -> Blizzard in Cyan
        [34660] = [34659], // Stone II in Yellow -> Blizzard II in Cyan
        [34661] = [34659], // Thunder II in Magenta -> Blizzard II in Cyan
        [34664] = [34689], // Pom Motif -> Creature Motif
        [34665] = [34689], // Wing Motif -> Creature Motif
        [34666] = [34689], // Claw Motif -> Creature Motif
        [34667] = [34689], // Maw Motif -> Creature Motif
        [34668] = [34690], // Hammer Motif -> Weapon Motif
        [34669] = [34691], // Starry Sky Motif -> Landscape Motif
        [34663] = [34662], // Comet in Black -> Holy in White
        // SAM
        [7487] = [7867], // Midare Setsugekka -> Iaijutsu
        [7488] = [7867], // Tenka Goken -> Iaijutsu
        [7489] = [7867], // Higanbana -> Iaijutsu
        [36965] = [7867], // Tendo Goken -> Iaijutsu
        [36966] = [7867], // Tendo Setsugekka -> Iaijutsu
        [16485] = [16483], // Kaeshi: Goken -> Tsubame-gaeshi
        [16486] = [16483], // Kaeshi: Setsugekka -> Tsubame-gaeshi
        [36967] = [16483], // Tendo Kaeshi Goken -> Tsubame-gaeshi
        [36968] = [16483], // Tendo Kaeshi Setsugekka -> Tsubame-gaeshi
        [25782] = [25781], // Kaeshi: Namikiri -> Ogi Namikiri
        // NIN
        [2265] = [2260], // Fuma Shuriken -> Ninjutsu
        [18873] = [2260], // Fuma Shuriken -> Ninjutsu
        [18874] = [2260], // Fuma Shuriken -> Ninjutsu
        [18875] = [2260], // Fuma Shuriken -> Ninjutsu
        [2266] = [2260], // Katon -> Ninjutsu
        [18876] = [2260], // Katon -> Ninjutsu
        [2267] = [2260], // Raiton -> Ninjutsu
        [18877] = [2260], // Raiton -> Ninjutsu
        [2268] = [2260], // Hyoton -> Ninjutsu
        [18878] = [2260], // Hyoton -> Ninjutsu
        [2269] = [2260], // Huton -> Ninjutsu
        [18879] = [2260], // Huton -> Ninjutsu
        [2270] = [2260], // Doton -> Ninjutsu
        [18880] = [2260], // Doton -> Ninjutsu
        [2271] = [2260], // Suiton -> Ninjutsu
        [18881] = [2260], // Suiton -> Ninjutsu
        [2272] = [2260], // Rabbit Medium -> Ninjutsu
        [16491] = [2260], // Goka Mekkyaku -> Ninjutsu
        [16492] = [2260], // Hyosho Ranryu -> Ninjutsu
        [25774] = [16493], // Phantom Kamaitachi -> Bunshin
        [25775] = [16493], // Phantom Kamaitachi -> Bunshin
        [36961] = [7403], // Tenri Jindo -> Ten Chi Jin
        [36959] = [7401], // Deathfrog Medium -> Hellfrog Medium
        // MNK
        [3545] = [25764], // Elixir Field -> Masterful Blitz
        [36948] = [25764], // Elixir Burst -> Masterful Blitz
        [25765] = [25764], // Celestial Revolution -> Masterful Blitz
        [25768] = [25764], // Rising Phoenix -> Masterful Blitz
        [25882] = [25764], // Flint Strike -> Masterful Blitz
        [25769] = [25764], // Phantom Rush -> Masterful Blitz
        [3543] = [25764], // Tornado Kick -> Masterful Blitz
        // DRG
        [16479] = [75], // Raiden Thrust -> True Thrust
        [25770] = [86], // Draconian Fury -> Doom Spike
        // DRK
        [36928] = [7392], // Scarlet Delirium -> Bloodspiller
        [36929] = [7392], // Comeuppance -> Bloodspiller
        [36930] = [7392], // Torcleaver -> Bloodspiller
        [36931] = [7391], // Impalement -> Quietus
        [25755] = [3639], // Salt and Darkness -> Salted Earth
        [25756] = [3639], // Salt and Darkness -> Salted Earth
        // WAR
        [16465] = [3549], // Inner Chaos -> Fell Cleave
        [16463] = [3550], // Chaotic Cyclone -> Decimate
        [36924] = [7389], // Primal Wrath -> Inner Release
        [36925] = [25753], // Primal Ruination -> Primal Rend
        // PLD
        [36918] = [16460], // Supplication -> Atonement
        [36919] = [16460], // Sepulchre -> Atonement
        [36922] = [36921, 7383], // Blade of Honor -> Imperator, Requiescat
        // RPR
        [24395] = [24382], // Void Reaping -> Gibbet
        [36970] = [24382], // Executioner's Gibbet -> Gibbet
        [24396] = [24383], // Cross Reaping -> Gallows
        [36971] = [24383], // Executioner's Gallows -> Gallows
        [24397] = [24384], // Grim Reaping -> Guillotine
        [36972] = [24384], // Executioner's Guillotine -> Guillotine
        [24390] = [24389], // Unveiled Gibbet -> Blood Stalk
        [24391] = [24389], // Unveiled Gallows -> Blood Stalk
        [24399] = [24389], // Lemure's Slice -> Blood Stalk
        [24400] = [24392], // Lemure's Scythe -> Grim Swathe
        [36973] = [24398], // Perfectio -> Communio
        [24388] = [24387], // Harvest Moon -> Soulsow
        [24403] = [24401, 24402], // Regress -> Hell's Ingress, Hell's Egress
        [36969] = [24393], // Sacrificium -> Gluttony
        // BRD
        [25784] = [16496], // Blast Arrow -> Apex Arrow
        // MCH
        [36981] = [25788], // Excavator -> Chain Saw
        // DNC
        [15999] = [15989], // Emboite -> Cascade
        [16000] = [15990], // Entrechat -> Fountain
        [16001] = [15991], // Jete -> Reverse Cascade
        [16002] = [15992], // Pirouette -> Fountainfall
        [16003] = [15997], // Standard Finish -> Standard Step
        [36984] = [15997], // Finishing Move -> Standard Step
        [16004] = [15998], // Technical Finish -> Technical Step
        [25790] = [15998], // Tillana -> Technical Step
        [36985] = [16005], // Dance of the Dawn -> Saber Dance
        [25789] = [16014], // Improvised Finish -> Improvisation
        // BLM
        [25797] = [141, 142], // Paradox -> Fire, Blizzard
        // RDM
        [7527] = [7504], // Enchanted Riposte -> Riposte
        [45960] = [7504], // Enchanted Riposte -> Riposte
        [7528] = [7512], // Enchanted Zwerchhau -> Zwerchhau
        [45961] = [7512], // Enchanted Zwerchhau -> Zwerchhau
        [7529] = [7516], // Enchanted Redoublement -> Redoublement
        [45962] = [7516], // Enchanted Redoublement -> Redoublement
        [7530] = [7513], // Enchanted Moulinet -> Moulinet
        [37002] = [7513], // Enchanted Moulinet Deux -> Moulinet
        [37003] = [7513], // Enchanted Moulinet Trois -> Moulinet
        [16528] = [16529], // Enchanted Reprise -> Reprise
        // SMN
        [25823] = [25883], // Ruby Rite -> Gemshine
        [25824] = [25883], // Topaz Rite -> Gemshine
        [25825] = [25883], // Emerald Rite -> Gemshine
        [25808] = [25883], // Ruby Ruin -> Gemshine
        [25809] = [25883], // Topaz Ruin -> Gemshine
        [25810] = [25883], // Emerald Ruin -> Gemshine
        [25811] = [25883], // Ruby Ruin II -> Gemshine
        [25812] = [25883], // Topaz Ruin II -> Gemshine
        [25813] = [25883], // Emerald Ruin II -> Gemshine
        [25817] = [25883], // Ruby Ruin III -> Gemshine
        [25818] = [25883], // Topaz Ruin III -> Gemshine
        [25819] = [25883], // Emerald Ruin III -> Gemshine
        [25814] = [25884], // Ruby Outburst -> Precious Brilliance
        [25815] = [25884], // Topaz Outburst -> Precious Brilliance
        [25816] = [25884], // Emerald Outburst -> Precious Brilliance
        [25827] = [25884], // Ruby Disaster -> Precious Brilliance
        [25828] = [25884], // Topaz Disaster -> Precious Brilliance
        [25829] = [25884], // Emerald Disaster -> Precious Brilliance
        [25832] = [25884], // Ruby Catastrophe -> Precious Brilliance
        [25833] = [25884], // Topaz Catastrophe -> Precious Brilliance
        [25834] = [25884], // Emerald Catastrophe -> Precious Brilliance
        [25820] = [3579], // Astral Impulse -> Ruin III
        [36994] = [3579], // Umbral Impulse -> Ruin III
        [16514] = [3579], // Fountain of Fire -> Ruin III
        [25821] = [16511, 25826], // Astral Flare -> Outburst, Tri-disaster
        [36995] = [16511, 25826], // Umbral Flare -> Outburst, Tri-disaster
        [16515] = [16511, 25826], // Brand of Purgatory -> Outburst, Tri-disaster
        [3582] = [25822], // Deathflare -> Astral Flow
        [36996] = [25822], // Sunflare -> Astral Flow
        [25830] = [25822], // Rekindle -> Astral Flow
        [25835] = [25822], // Crimson Cyclone -> Astral Flow
        [25885] = [25822], // Crimson Strike -> Astral Flow
        [25836] = [25822], // Mountain Buster -> Astral Flow
        [25837] = [25822], // Slipstream -> Astral Flow
        [25831] = [7427], // Summon Phoenix -> Summon Bahamut
        [36992] = [7427], // Summon Solar Bahamut -> Summon Bahamut
        // SGE
        [24291] = [24284], // Eukrasian Diagnosis -> Diagnosis
        [24292] = [24286], // Eukrasian Prognosis -> Prognosis
        [37034] = [24286], // Eukrasian Prognosis II -> Prognosis
        [24293] = [24283, 24306, 24312], // Eukrasian Dosis -> Dosis, Dosis II, Dosis III
        [24308] = [24283, 24306, 24312], // Eukrasian Dosis II -> Dosis, Dosis II, Dosis III
        [24314] = [24283, 24306, 24312], // Eukrasian Dosis III -> Dosis, Dosis II, Dosis III
        [37032] = [24297, 24315], // Eukrasian Dyskrasia -> Dyskrasia, Dyskrasia II
        // SCH
        [16546] = [16545], // Consolation -> Summon Seraph
        [16547] = [16545], // Consolation -> Summon Seraph
        [37015] = [185], // Manifestation -> Adloquium
        [37016] = [37013], // Accession -> Concitation
        [7869] = [7437], // Dissolve Union -> Aetherpact
        // AST
        [8324] = [7439], // Stellar Detonation -> Earthly Star
    };
}
