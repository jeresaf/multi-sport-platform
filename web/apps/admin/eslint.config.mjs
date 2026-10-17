import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";
import base from "@msp/config/eslint";

const config = [...nextVitals, ...nextTs, ...base];

export default config;
