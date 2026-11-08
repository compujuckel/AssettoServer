# AssettoServer Crash Report generated on {{ timestamp }}
Server Version: {{ server_version }}  
Operating System: {{ os_version }} ({{ cpu_architecture }})  
Content Manager: {{ content_manager ? "Yes" : "No" }}  
{{ for attachment in attachments }}
## {{ attachment.name }}
```{{ attachment.type }}
{{ attachment.content }}
```
{{ end }}
