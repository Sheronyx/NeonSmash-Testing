#import <Foundation/Foundation.h>
#import <GameKit/GameKit.h>

#if defined(__cplusplus)
extern "C" {
#endif

extern void UnitySendMessage(const char* obj, const char* method, const char* msg);
static NSString* _Base64(NSData* data) { return [data base64EncodedStringWithOptions:0]; }

void GCRequestIdentitySignature(const char* unityObject, const char* unityMethod)
{
    NSString* obj = [NSString stringWithUTF8String:unityObject ?: ""];
    NSString* met = [NSString stringWithUTF8String:unityMethod ?: ""];
    GKLocalPlayer* localPlayer = GKLocalPlayer.localPlayer;

    void (^send)(NSDictionary*) = ^(NSDictionary* dict) {
        if (!dict) dict = @{};
        NSError* err = nil;
        NSData* json = [NSJSONSerialization dataWithJSONObject:dict options:0 error:&err];
        if (err || !json) { UnitySendMessage(obj.UTF8String, met.UTF8String, "{}"); return; }
        NSString* s = [[NSString alloc] initWithData:json encoding:NSUTF8StringEncoding];
        UnitySendMessage(obj.UTF8String, met.UTF8String, s.UTF8String);
    };

    [localPlayer setAuthenticateHandler:^(UIViewController* vc, NSError* error) {
        if (error) { send(@{ @"error": error.localizedDescription ?: @"auth_error" }); return; }
        if (vc) return; // Systemdialog wird gezeigt
        if (!localPlayer.isAuthenticated) { send(@{ @"error": @"not_authenticated" }); return; }

        NSString* teamPlayerId = @"";
        if (@available(iOS 13.0, *)) teamPlayerId = localPlayer.teamPlayerID ?: @"";

        [localPlayer generateIdentityVerificationSignatureWithCompletionHandler:
         ^(NSURL* pkURL, NSData* sig, NSData* salt, uint64_t ts, NSError* genErr)
        {
            if (genErr || !sig || !salt || !pkURL)
            { send(@{ @"error": genErr.localizedDescription ?: @"signature_failed" }); return; }

            NSDictionary* payload = @{
                @"signature": _Base64(sig) ?: @"",
                @"salt": _Base64(salt) ?: @"",
                @"timestamp": @(ts),
                @"publicKeyURL": pkURL.absoluteString ?: @"",
                @"teamPlayerId": teamPlayerId ?: @""
            };
            send(payload);
        }];
    }];
}

#if defined(__cplusplus)
}
#endif
