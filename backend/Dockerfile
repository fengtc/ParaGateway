FROM golang:1.26.6-alpine AS builder

WORKDIR /app

# Git is used by version/build helpers.
RUN apk add --no-cache git

# 复制go.mod和go.sum
COPY go.mod go.sum ./

# 下载依赖
RUN go mod download

# 复制源代码
COPY . .

# 构建应用
RUN VERSION_VALUE="$(sh ./scripts/resolve-version.sh)" && \
    go build -ldflags="-s -w -X main.Version=${VERSION_VALUE}" -o main ./cmd/server/

FROM alpine:3.24

WORKDIR /app

# HTTPS upstreams require the CA bundle, and configured Asia/Shanghai time
# requires the Alpine timezone database at runtime.
RUN apk add --no-cache ca-certificates tzdata

COPY --from=builder /app/main ./main

# 暴露端口
EXPOSE 8080

# 运行应用
CMD ["./main"]
