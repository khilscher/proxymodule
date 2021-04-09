# Proxy Module
Sample HTTP/S proxy module for IoT Edge written in .NET Core. The proxy module utilizes [Privoxy](https://www.privoxy.org/) as the HTTP/S proxy. 

# Setup
- Module was developed for amd64 so only the **Dockerfile.amd64** contains the docker configuration. If you build for other chipsets, copy the contents of **Dockerfile.amd64** to the appropriate Dockerfile.nnn file.
- Module is configured to listen on port TCP/3129. This can be changed by editing the following three files:
  - **Dockerfile.nnn** and changing the ```EXPOSE``` port
  - **Deployment.template.json** and changing the ```Portbindings```
  - **Config** and changing the ```listen-address```
- By default, the module is configured to forward all HTTP/S proxy requests to another Proxy server address (default: forward / 1.1.1.1:3129). This can be modifying using the module twin's ```Forward``` desired property (described in the next sections), which also updates the Privoxy config file accordingly.

# Enable Forwarding (aka proxy chaining)

e.g. HTTP/S client -> proxy module on IoT Edge #1 -> proxy module on IoT Edge #2 -> Internet

To forward HTTP/S from the proxy module on IoT Edge #1 to the proxy module on IoT Edge #2 (IP: 192.168.10.11:3129), edit the module twin settings of the proxy module on IoT Edge #1 as follows:

```
    "properties": {
        "desired": {
            "Forward": "forward / 192.168.10.11:3129",
            "$metadata": …
```

Configure the module twin settings on the proxy module on IoT Edge #2 to not forward and send directly to the Internet:

```
    "properties": {
        "desired": {
            "Forward": null,
            "$metadata": …
```


# Disabling Forwarding (aka proxy chaining)

e.g. HTTP/S client -> proxy module on IoT Edge #1 -> Internet

To remove any forwarding and send all inbound HTTP/S requests to the Internet...

```
    "properties": {
        "desired": {
            "Forward": null,
            "$metadata": …
```
- Note: Setting ```Forward``` to ```null``` removes it from the module twin's desired properties and comments out the foward rule in the Privoxy config file.

# Testing
- ```curl -x "http://<ip_of_IoT_Edge_device_running_proxy_module>:3129" "https://www.google.com"```
