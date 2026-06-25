import { cartService } from '../services/cartService';

export const cartController = {
  get(req: any, res: any) {
    res.json(cartService.find(req.params.id));
  },
  addItem(req: any, res: any) {
    res.json(cartService.addItem(req.params.id, req.body));
  },
  clear(req: any, res: any) {
    cartService.clear(req.params.id);
    res.status(204).end();
  }
};
