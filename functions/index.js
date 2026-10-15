const { onRequest }     = require("firebase-functions/v2/https");
const { onSchedule }    = require("firebase-functions/v2/scheduler");
const { initializeApp } = require("firebase-admin/app");
const { getMessaging }  = require("firebase-admin/messaging");
const { getFirestore }  = require("firebase-admin/firestore");

initializeApp();

const CLOUD_CODE_SECRET = process.env.CLOUD_CODE_SECRET;

// Obergrenze gleichzeitig laufender Instanzen. Ohne die skaliert Cloud Run im Missbrauchsfall
// beliebig hoch -- jeder Aufruf kostet einen Funktionsaufruf und einen Firestore-Schreibvorgang.
// Die Werte sind fuer den normalen Betrieb reichlich bemessen: eine Instanz bedient viele
// Anfragen nacheinander, 10 gleichzeitige reichen fuer ein Vielfaches der erwarteten Last.
const LIMIT_OFFEN     = { maxInstances: 10 };  // vom Spiel aufgerufen, nicht abgesichert
const LIMIT_GESCHUETZT = { maxInstances: 5 };  // nur mit Geheimnis aufrufbar
const LIMIT_GEPLANT   = { maxInstances: 1 };   // darf sich nie selbst ueberholen

// Called from Unity when the FCM token is obtained / refreshed
exports.registerFcmToken = onRequest(LIMIT_OFFEN, async (req, res) => {
  if (req.method !== "POST") {
    res.status(405).json({ error: "Method not allowed" });
    return;
  }

  const { playerId, fcmToken } = req.body;
  if (!playerId || !fcmToken) {
    res.status(400).json({ success: false, reason: "missing_params" });
    return;
  }

  try {
    await getFirestore()
      .collection("fcm_tokens")
      .doc(playerId)
      .set({ token: fcmToken, updatedAt: Date.now(), lastSeen: Date.now() });
    res.json({ success: true });
  } catch (e) {
    console.error("Firestore write failed:", e.message);
    res.status(500).json({ success: false, reason: e.message });
  }
});

// Called from the game right before the Unity account is deleted.
// Body: { playerId, fcmToken }
//
// Why the token must be supplied: this endpoint is called from the client, so there is no
// trustworthy caller identity (same limitation as registerFcmToken). Requiring the token means
// a caller can only delete entries whose token they already hold -- in practice, their own
// device. That is a mitigation, not authentication; see the note in the README.
//
// Why we also query by token: when an anonymous account is later linked to Play Games or Game
// Center, the Unity PlayerID changes and the token gets written again under the new ID. The old
// document stays behind. Deleting only doc(playerId) would leave it there forever.
exports.deleteFcmToken = onRequest(LIMIT_OFFEN, async (req, res) => {
  if (req.method !== "POST") {
    res.status(405).json({ error: "Method not allowed" });
    return;
  }

  const { playerId, fcmToken } = req.body;
  if (!playerId || !fcmToken) {
    res.status(400).json({ success: false, reason: "missing_params" });
    return;
  }

  try {
    const db = getFirestore();
    const collection = db.collection("fcm_tokens");
    const deleted = [];

    // 1) Der Eintrag zur uebergebenen PlayerID -- nur wenn der Token uebereinstimmt.
    const direct = await collection.doc(playerId).get();
    if (direct.exists && direct.data()?.token === fcmToken) {
      await direct.ref.delete();
      deleted.push(playerId);
    }

    // 2) Alle weiteren Eintraege mit demselben Token (Altlasten aus ID-Wechseln).
    const duplicates = await collection.where("token", "==", fcmToken).get();
    for (const doc of duplicates.docs) {
      if (deleted.includes(doc.id)) continue;
      await doc.ref.delete();
      deleted.push(doc.id);
    }

    console.log("Deleted " + deleted.length + " token document(s): " + deleted.join(", "));
    res.json({ success: true, deleted: deleted.length });
  } catch (e) {
    console.error("Firestore delete failed:", e.message);
    res.status(500).json({ success: false, reason: e.message });
  }
});

// Called from Unity Cloud Code with { recipientPlayerId, senderName }
exports.sendChallengeNotification = onRequest(LIMIT_GESCHUETZT, async (req, res) => {
  if (req.method !== "POST") {
    res.status(405).json({ error: "Method not allowed" });
    return;
  }

  if (req.headers["x-secret"] !== CLOUD_CODE_SECRET) {
    res.status(403).json({ success: false, reason: "forbidden" });
    return;
  }

  const { recipientPlayerId, senderName } = req.body;
  if (!recipientPlayerId) {
    res.status(400).json({ success: false, reason: "missing_recipient" });
    return;
  }

  let fcmToken;
  try {
    const doc = await getFirestore()
      .collection("fcm_tokens")
      .doc(recipientPlayerId)
      .get();
    fcmToken = doc.data()?.token;
  } catch (e) {
    console.error("Firestore read failed:", e.message);
    res.status(500).json({ success: false, reason: "firestore_error" });
    return;
  }

  if (!fcmToken) {
    console.log("No FCM token for player " + recipientPlayerId);
    res.status(404).json({ success: false, reason: "no_token" });
    return;
  }

  try {
    await getMessaging().send({
      token: fcmToken,
      notification: {
        title: "NeonSmash Challenge!",
        body:  (senderName || "Someone") + " challenges you to a duel!"
      },
      data: { type: "challenge" }
    });
    res.json({ success: true });
  } catch (e) {
    console.error("FCM send failed:", e.message);

    // Hat der Nutzer die Benachrichtigungen im System abgeschaltet oder die App entfernt,
    // meldet FCM den Token als nicht mehr registriert. Dann hat er bei uns nichts mehr zu
    // suchen -- gleich loeschen statt auf die Aufbewahrungsfrist zu warten.
    const abgemeldet = e.code === "messaging/registration-token-not-registered" ||
                       e.code === "messaging/invalid-registration-token";
    if (abgemeldet) {
      try {
        await getFirestore().collection("fcm_tokens").doc(recipientPlayerId).delete();
        console.log("Abgemeldeten Token entfernt: " + recipientPlayerId);
      } catch (loeschFehler) {
        console.error("Token-Loeschung fehlgeschlagen:", loeschFehler.message);
      }
    }

    res.status(500).json({ success: false, reason: e.message });
  }
});

// ---------------------------------------------------------------------------
// Aufbewahrungsfrist fuer Push-Tokens
//
// Laeuft taeglich und entfernt Token-Eintraege, die seit INAKTIV_TAGE nicht mehr erneuert
// wurden. Das faengt zwei Faelle ab, die der Loeschknopf nicht abdeckt:
//   - Spieler, die einfach deinstalliert haben, ohne ihr Konto zu loeschen
//   - Altlasten aus ID-Wechseln, deren Token inzwischen von FCM rotiert wurde
//
// 730 Tage (24 Monate) ist seit dem 15.10.2026 die anwaltlich festgelegte Frist. Wer den Wert
// aendert, muss die Datenschutzerklaerung mitaendern -- die Zahl steht dort.
//
// Gezaehlt wird ab `updatedAt`, und das schreibt registerFcmToken bei JEDEM App-Start neu.
// Die Frist laeuft also ab der letzten Nutzung, nicht ab der ersten Registrierung.
const INAKTIV_TAGE = 730;

exports.cleanupStaleFcmTokens = onSchedule(
  { schedule: "every 24 hours", ...LIMIT_GEPLANT },
  async () => {
  const grenze = Date.now() - INAKTIV_TAGE * 24 * 60 * 60 * 1000;

  const alte = await getFirestore()
    .collection("fcm_tokens")
    .where("updatedAt", "<", grenze)
    .get();

  if (alte.empty) {
    console.log("Keine veralteten Token-Eintraege.");
    return;
  }

  // In Stapeln loeschen -- Firestore erlaubt 500 Schreibvorgaenge pro Stapel.
  let geloescht = 0;
  let stapel = getFirestore().batch();
  let imStapel = 0;

  for (const doc of alte.docs) {
    stapel.delete(doc.ref);
    imStapel++;
    geloescht++;
    if (imStapel === 500) {
      await stapel.commit();
      stapel = getFirestore().batch();
      imStapel = 0;
    }
  }
  if (imStapel > 0) await stapel.commit();

    console.log("Veraltete Token-Eintraege geloescht: " + geloescht
              + " (aelter als " + INAKTIV_TAGE + " Tage)");
  }
);

// ---------------------------------------------------------------------------
// Loeschung inaktiver Spielerkonten
//
// Warum ueberhaupt ueber Firestore: Unity bietet KEINE Schnittstelle, um Spieler nach
// Inaktivitaet aufzulisten -- nur das Loeschen EINES bekannten Spielers. Deshalb spiegelt das
// Spiel bei jedem Start seine PlayerID mit Zeitstempel hierher, und erst diese Liste laesst
// sich abfragen.
//
// 36 Monate (Stand 15.10.2026): bewusst laenger als bei den Push-Token. Spieler kaufen Welten
// und Skins mit echtem Geld; eine Loeschung vernichtet diese Inhalte unwiderruflich. Drei Jahre
// Inaktivitaet sind lang genug, dass niemand wegen einer Pause sein bezahltes Spiel verliert.
// Die Zahl muss mit der Datenschutzerklaerung uebereinstimmen.
const INAKTIV_MONATE = 36;

// Solange true wird NICHTS geloescht -- es wird nur protokolliert, wen es treffen wuerde.
// Erst umstellen, wenn die Liste in den Logs plausibel aussieht.
const TROCKENLAUF = true;

// Hoechstens so viele Spieler pro Lauf. Begrenzt den Schaden, falls etwas falsch laeuft, und
// haelt die Laufzeit im Rahmen. Bei taeglichem Lauf ist das reichlich.
const MAX_PRO_LAUF = 200;

const UNITY_BASIS = "https://services.api.unity.com";

// Projekt- und Umgebungs-ID sind keine Geheimnisse (die Projekt-ID steht in den ProjectSettings
// im Repo). Nur Schluessel-ID und Geheimnis des Dienstkontos gehoeren in die Geheimnisverwaltung.
const UNITY_PROJECT_ID = "0f04c797-5899-4aaa-9ae9-f85196f8f0dd";
// Umgebung "production". Die Entwicklungsumgebung (a1f8cd11-31c6-4d84-a151-ffc5a61a9753)
// bewusst NICHT -- dort darf nichts automatisch geloescht werden.
const UNITY_ENV_ID = "a0c0bfd1-dc31-40d0-be8f-d526e07f62c3";

function unityKopf() {
  const id     = process.env.UNITY_KEY_ID;
  const secret = process.env.UNITY_SECRET;
  if (!id || !secret) throw new Error("UNITY_KEY_ID/UNITY_SECRET fehlen");
  return {
    Authorization: "Basic " + Buffer.from(id + ":" + secret).toString("base64"),
    "Content-Type": "application/json"
  };
}

// Vom Spiel bei jedem Start aufgerufen. Haelt fest, wann die PlayerID zuletzt aktiv war.
exports.registerActivity = onRequest(LIMIT_OFFEN, async (req, res) => {
  if (req.method !== "POST") {
    res.status(405).json({ error: "Method not allowed" });
    return;
  }

  const { playerId } = req.body;
  if (!playerId) {
    res.status(400).json({ success: false, reason: "missing_params" });
    return;
  }

  try {
    await getFirestore()
      .collection("players")
      .doc(playerId)
      .set({ lastSeen: Date.now() }, { merge: true });
    res.json({ success: true });
  } catch (e) {
    console.error("Activity write failed:", e.message);
    res.status(500).json({ success: false, reason: e.message });
  }
});

// Gegenstueck: entfernt den Aktivitaetseintrag, wenn der Spieler sein Konto loescht.
// Wie bei registerFcmToken gibt es hier keine belastbare Aufruferidentitaet -- wer eine fremde
// PlayerID kennt, koennte deren Eintrag loeschen. Der Schaden waere begrenzt (der Eintrag wird
// beim naechsten Start neu geschrieben), aber App Check gehoert vor dem Release auf beide
// offenen Endpunkte.
exports.deleteActivity = onRequest(LIMIT_OFFEN, async (req, res) => {
  if (req.method !== "POST") {
    res.status(405).json({ error: "Method not allowed" });
    return;
  }

  const { playerId } = req.body;
  if (!playerId) {
    res.status(400).json({ success: false, reason: "missing_params" });
    return;
  }

  try {
    await getFirestore().collection("players").doc(playerId).delete();
    res.json({ success: true });
  } catch (e) {
    console.error("Activity delete failed:", e.message);
    res.status(500).json({ success: false, reason: e.message });
  }
});

// Loescht die Cloud-Save-Daten eines Spielers. Die Kontoloeschung bei Unity macht das NICHT
// mit -- das steht so in Unitys eigener Doku und ist ausdruecklich unsere Pflicht.
async function cloudSaveLoeschen(projectId, envId, playerId) {
  const pfad = UNITY_BASIS + "/cloud-save/v1/data/projects/" + projectId
             + "/environments/" + envId + "/players/" + playerId + "/items";
  const antwort = await fetch(pfad, { method: "DELETE", headers: unityKopf() });
  // 404 heisst: da war nichts mehr. Das ist kein Fehler.
  if (!antwort.ok && antwort.status !== 404) {
    throw new Error("Cloud Save " + antwort.status + ": " + (await antwort.text()).slice(0, 200));
  }
}

async function kontoLoeschen(projectId, playerId) {
  const pfad = UNITY_BASIS + "/player-identity/v1/projects/" + projectId + "/users/" + playerId;
  const antwort = await fetch(pfad, { method: "DELETE", headers: unityKopf() });
  if (!antwort.ok && antwort.status !== 404) {
    throw new Error("Player-Identity " + antwort.status + ": " + (await antwort.text()).slice(0, 200));
  }
}

exports.cleanupInactivePlayers = onSchedule(
  { schedule: "every 24 hours", ...LIMIT_GEPLANT, secrets: ["UNITY_KEY_ID", "UNITY_SECRET"] },
  async () => {
    const projectId = UNITY_PROJECT_ID;
    const envId     = UNITY_ENV_ID;
    if (!projectId || !envId) {
      console.error("UNITY_PROJECT_ID/UNITY_ENV_ID fehlen -- Lauf abgebrochen.");
      return;
    }

    const grenze = Date.now() - INAKTIV_MONATE * 30.44 * 24 * 60 * 60 * 1000;

    const alte = await getFirestore()
      .collection("players")
      .where("lastSeen", "<", grenze)
      .limit(MAX_PRO_LAUF)
      .get();

    if (alte.empty) {
      console.log("Keine inaktiven Spieler.");
      return;
    }

    if (TROCKENLAUF) {
      console.log("TROCKENLAUF -- es wird nichts geloescht. Betroffen waeren "
                + alte.size + " Spieler:");
      for (const doc of alte.docs) {
        const tage = Math.round((Date.now() - doc.data().lastSeen) / 86400000);
        console.log("  " + doc.id + "  zuletzt vor " + tage + " Tagen");
      }
      return;
    }

    let erledigt = 0, fehler = 0;
    for (const doc of alte.docs) {
      try {
        // Reihenfolge: erst die Daten, dann das Konto. Umgekehrt waere die PlayerID weg,
        // bevor die Cloud-Save-Daten geloescht sind -- und damit nicht mehr auffindbar.
        await cloudSaveLoeschen(projectId, envId, doc.id);
        await kontoLoeschen(projectId, doc.id);
        await doc.ref.delete();
        erledigt++;
      } catch (e) {
        fehler++;
        console.error("Loeschung fehlgeschlagen fuer " + doc.id + ": " + e.message);
      }
    }

    console.log("Inaktive Spieler geloescht: " + erledigt + ", fehlgeschlagen: " + fehler
              + " (Frist: " + INAKTIV_MONATE + " Monate)");
  }
);
