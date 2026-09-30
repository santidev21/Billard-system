FROM node:22-alpine

WORKDIR /app

COPY package.json ./
COPY src ./src

ENV NODE_ENV=production
EXPOSE 8089

CMD ["node", "src/index.js"]
