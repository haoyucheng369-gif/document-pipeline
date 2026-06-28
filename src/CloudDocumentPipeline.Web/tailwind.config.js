export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        ink: "#112015",
        mist: "#f4f6f2",
        line: "#d7ddd2",
        accent: "#28543a",
        soft: "#eef3ec"
      },
      boxShadow: {
        card: "0 16px 40px rgba(17, 32, 21, 0.08)"
      }
    }
  },
  plugins: []
};
