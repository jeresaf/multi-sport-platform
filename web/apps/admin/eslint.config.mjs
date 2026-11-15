import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";
import { apps } from "@msp/config/eslint";

const config = [...nextVitals, ...nextTs, ...apps];

export default config;
