const { onRequest }     = require("firebase-functions/v2/https");
const { initializeApp } = require("firebase-admin/app");
const { getMessaging }  = require("firebase-admin/messaging");
const { getFirestore }  = require("firebase-admin/firestore");

initializeApp();

const CLOUD_CODE_SECRET = process.env.CLOUD_CODE_SECRET;

// Called from Unity when the FCM token is obtained / refreshed
exports.registerFcmToken = onRequest(async (req, res) => {
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
      .set({ token: fcmToken, updatedAt: Date.now() });
    res.json({ success: true });
  } catch (e) {
    console.error("Firestore write failed:", e.message);
    res.status(500).json({ success: false, reason: e.message });
  }
});

// Called from Unity Cloud Code with { recipientPlayerId, senderName }
exports.sendChallengeNotification = onRequest(async (req, res) => {
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
    res.status(500).json({ success: false, reason: e.message });
  }
});
