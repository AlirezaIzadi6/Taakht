import base64, hmac, hashlib, json, time
secret = b'spike-test-secret-spike-test-0123'
b = lambda x: base64.urlsafe_b64encode(x).rstrip(b'=')
h = b(json.dumps({"alg":"HS256","typ":"JWT"}).encode())
p = b(json.dumps({"iss":"spike-issuer","sub":"user-42","exp":int(time.time())+3600}).encode())
print((h+b'.'+p+b'.'+b(hmac.new(secret, h+b'.'+p, hashlib.sha256).digest())).decode())
