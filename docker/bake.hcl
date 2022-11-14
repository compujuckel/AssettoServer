target "docker-metadata-action" {}

target "default" {
  inherits = ["docker-metadata-action"]
  context = "./"
  dockerfile = "./docker/Dockerfile"
  platforms = [
    "linux/amd64",
    "linux/arm64",
  ]
}

