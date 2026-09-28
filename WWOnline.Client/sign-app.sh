#!/bin/bash

# Sign the WW-Online app with debugging entitlements
# This allows the app to read/write memory of other processes (like Dolphin)

echo "🔐 Signing WW-Online with debugging entitlements..."

# Path to the built executable
APP_PATH="./bin/Debug/net9.0/WWOnline"
ENTITLEMENTS_PATH="./entitlements.plist"

# Check if the app exists
if [ ! -f "$APP_PATH" ]; then
    echo "❌ Error: Application not found at $APP_PATH"
    echo "Please run 'dotnet build' first"
    exit 1
fi

# Check if entitlements file exists
if [ ! -f "$ENTITLEMENTS_PATH" ]; then
    echo "❌ Error: Entitlements file not found at $ENTITLEMENTS_PATH"
    exit 1
fi

# Create a self-signed certificate if needed (you'll only need to do this once)
# The certificate will be stored in your keychain
CERT_NAME="WWOnline Developer"

# Check if certificate already exists
if security find-identity -v -p codesigning | grep -q "$CERT_NAME"; then
    echo "✅ Certificate '$CERT_NAME' already exists"
    echo "🔄 Removing old certificate to create a fresh one..."
    security delete-certificate -c "$CERT_NAME" 2>/dev/null || true
fi

# Always create a fresh certificate for now
if true; then
    echo "📝 Creating self-signed certificate..."
    
    # Create a certificate signing request and self-signed certificate
    cat > /tmp/cert-config.txt << EOF
[ req ]
distinguished_name = req_distinguished_name
x509_extensions = v3_ca

[ req_distinguished_name ]
CN = $CERT_NAME

[ v3_ca ]
keyUsage = critical, digitalSignature, cRLSign, keyCertSign
basicConstraints = critical, CA:TRUE
extendedKeyUsage = critical, codeSigning
EOF

    # Generate the certificate
    openssl req -new -x509 -days 3650 -nodes \
        -config /tmp/cert-config.txt \
        -subj "/CN=$CERT_NAME" \
        -keyout /tmp/cert.key \
        -out /tmp/cert.crt
    
    # Convert to P12 format for importing to keychain
    openssl pkcs12 -export \
        -in /tmp/cert.crt \
        -inkey /tmp/cert.key \
        -out /tmp/cert.p12 \
        -passout pass:temppass \
        -legacy
    
    # Import into keychain
    security import /tmp/cert.p12 -P temppass -A -T /usr/bin/codesign
    
    # Clean up temp files
    rm /tmp/cert.key /tmp/cert.crt /tmp/cert.p12 /tmp/cert-config.txt
    
    echo "✅ Certificate created and imported to keychain"
fi

# Sign the application
echo "🖊️  Signing application..."
codesign --force --sign "$CERT_NAME" --entitlements "$ENTITLEMENTS_PATH" --deep "$APP_PATH"

# Verify the signature
echo "🔍 Verifying signature..."
codesign --verify --verbose "$APP_PATH"

# Check entitlements
echo "📋 Checking entitlements..."
codesign -d --entitlements - "$APP_PATH"

echo ""
echo "✅ Application signed successfully!"
echo ""
echo "⚠️  Important Notes:"
echo "1. You may need to approve the certificate in System Settings > Privacy & Security"
echo "2. You might see a popup asking to allow the app to control other apps"
echo "3. The app now has debugging privileges and can read Dolphin's memory"
echo ""
echo "To run the signed app:"
echo "  dotnet run"
echo ""
echo "Or run the executable directly:"
echo "  $APP_PATH"