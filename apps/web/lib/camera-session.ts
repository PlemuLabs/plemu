/** One opt-in camera owner. Late permissions cannot reactivate a stopped session. */
export function createCameraSession() {
  let generation = 0;
  let current: MediaStream | null = null;
  const release = (stream: MediaStream) =>
    stream.getTracks().forEach((track) => track.stop());
  const stop = () => {
    generation += 1;
    if (current) release(current);
    current = null;
  };
  return {
    stop,
    get active() {
      return current !== null;
    },
    async request(acquire: () => Promise<MediaStream>) {
      stop();
      const requestGeneration = generation;
      try {
        const stream = await acquire();
        if (generation !== requestGeneration) {
          release(stream);
          return null;
        }
        current = stream;
        return stream;
      } catch (error) {
        if (generation !== requestGeneration) return null;
        throw error;
      }
    },
  };
}
