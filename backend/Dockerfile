FROM golang:1.26.6-alpine

WORKDIR /app

# Git is used by version/build helpers; tzdata is required when the configured
# application timezone is Asia/Shanghai (the Alpine Go image does not ship it).
RUN apk add --no-cache git tzdata

# 复制go.mod和go.sum
COPY go.mod go.sum ./

# 下载依赖
RUN go mod download

# 复制源代码
COPY . .

# 构建应用
RUN VERSION_VALUE="$(sh ./scripts/resolve-version.sh)" && \
    go build -ldflags="-s -w -X main.Version=${VERSION_VALUE}" -o main ./cmd/server/

# 暴露端口
EXPOSE 8080

# 运行应用
CMD ["./main"]
